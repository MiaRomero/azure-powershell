﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Commands.PowerBIEmbeddedCapacity.Dataplane.Models;
using Microsoft.Azure.Commands.PowerBIEmbeddedCapacity.Dataplane.Properties;
using Microsoft.Azure.Commands.Common.Authentication.Abstractions;
using Microsoft.WindowsAzure.Commands.Common;
using Microsoft.WindowsAzure.Commands.Utilities.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.Commands.PowerBIEmbeddedCapacity.Dataplane
{
    /// <summary>
    /// Cmdlet to log into an PowerBI Embedded Capacity environment
    /// </summary>
    [Cmdlet(VerbsData.Sync, "AzurePowerBIEmbeddedCapacityInstance", SupportsShouldProcess = true)]
    [Alias("Sync-AzureCapacityInstance")]
    [OutputType(typeof(ScaleOutCapacityDatabaseSyncDetails[]))]
    public class SynchronizeAzurePowerBIEmbeddedCapacity : AzurePSCmdlet
    {
        private static TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(30);

        private static TimeSpan DefaultRetryIntervalForPolling = TimeSpan.FromSeconds(10);

        private static string RootActivityIdHeaderName = "x-ms-root-activity-id";

        private static string CurrentUtcDateHeaderName = "x-ms-current-utc-date";

        private string capacityName;

        private ClusterResolutionResult clusterResolveResult;

        private Guid correlationId;

        private string syncRequestRootActivityId;

        private string syncRequestTimeStamp;

        [Parameter(
            Mandatory = true,
            HelpMessage = "Name of the Azure PowerBI Embedded Capacity to synchronize. E.x. pbidedicated://westus.pbidedicated.windows.net/contosocapacity:rw",
            Position = 0,
            ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public string Instance { get; set; }

        [Parameter(
            Mandatory = true,
            HelpMessage = "Identity of the database need to be synchronized",
            Position = 1,
            ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public string Database { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter PassThru { get; set; }

        protected override IAzureContext DefaultContext
        {
            get
            {
                // Nothing to do with Azure Resource Management context
                return null;
            }
        }

        public IPBIHttpClient PBIHttpClient { get; private set; }

        public ITokenCacheItemProvider TokenCacheItemProvider { get; private set; }

        public SynchronizeAzurePowerBIEmbeddedCapacity()
        {
            this.PBIHttpClient = new PBIHttpClient(() =>
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler();
                httpClientHandler.AllowAutoRedirect = false;
                return new HttpClient(httpClientHandler);
            });

            this.TokenCacheItemProvider = new TokenCacheItemProvider();
            this.syncRequestRootActivityId = string.Empty;
            this.correlationId = Guid.Empty;
            this.syncRequestTimeStamp = string.Empty;
        }

        public SynchronizeAzurePowerBIEmbeddedCapacity(IPBIHttpClient PBIHttpClient, ITokenCacheItemProvider TokenCacheItemProvider)
        {
            this.PBIHttpClient = PBIHttpClient;
            this.TokenCacheItemProvider = TokenCacheItemProvider;
        }

        protected override void SetupDebuggingTraces()
        {
            // nothing to do here.
        }

        protected override void TearDownDebuggingTraces()
        {
            // nothing to do here.
        }

        protected override void SetupHttpClientPipeline()
        {
            // nothing to do here.
        }

        protected override void TearDownHttpClientPipeline()
        {
            // nothing to do here.
        }


        public override void ExecuteCmdlet()
        {
            if (ShouldProcess(Instance, Resources.SynchronizingPowerBIEmbeddedCapacity))
            {
                correlationId = Guid.NewGuid();
                WriteObject(string.Format("Sending sync request for database '{0}' to capacity '{1}'. Correlation Id: '{2}'.", Database, Instance, correlationId.ToString()));
                var context = PBIClientSession.Instance.Profile.Context;
                PBIClientSession.Instance.Login(context);
                WriteProgress(new ProgressRecord(0, "Sync-AzurePowerBIEmbeddedCapacityInstance.", string.Format("Authenticating user for '{0}' environment.", context.Environment.Name)));
                var clusterResolveResult = ClusterResolve(context, capacityName);
                if (!clusterResolveResult.CoreCapacityName.Equals(capacityName) || !clusterResolveResult.CoreCapacityName.EndsWith(":rw"))
                {
                    throw new SynchronizationFailedException("Sync request can only be sent to the management endpoint");
                }

                this.clusterResolveResult = clusterResolveResult;
                Uri clusterBaseUri = new Uri(string.Format("{0}{1}{2}", Uri.UriSchemeHttps, Uri.SchemeDelimiter, clusterResolveResult.ClusterFQDN));
                var accessToken = this.TokenCacheItemProvider.GetTokenFromTokenCache(PBIClientSession.TokenCache, context.Account.UniqueId, context.Environment.Name);

                ScaleOutCapacityDatabaseSyncDetails syncResult = null;
                try
                {
                    WriteProgress(new ProgressRecord(0, "Sync-AzurePowerBIEmbeddedCapacityInstance.", string.Format("Successfully authenticated for '{0}' environment.", context.Environment.Name)));
                    syncResult = SynchronizeDatabaseAsync(context, clusterBaseUri, Database, accessToken).GetAwaiter().GetResult();
                }
                catch (AggregateException aex)
                {
                    foreach (var innerException in aex.Flatten().InnerExceptions)
                    {
                        WriteExceptionError(innerException);
                    }
                }
                catch (Exception ex)
                {
                    WriteExceptionError(ex);
                }

                if (syncResult == null)
                {
                    throw new SynchronizationFailedException(string.Format(Resources.SyncASPollStatusUnknownMessage.FormatInvariant(
                        this.clusterResolveResult.CoreCapacityName,
                        correlationId,
                        DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        string.Format("RootActivityId: {0}, Date Time UTC: {1}", syncRequestRootActivityId, syncRequestTimeStamp))));
                }

                if (syncResult.SyncState != DatabaseSyncState.Completed)
                {
                    var serializedDetails = JsonConvert.SerializeObject(syncResult);
                    throw new SynchronizationFailedException(serializedDetails);
                }

                if (PassThru.IsPresent)
                {
                    WriteObject(syncResult, true);
                }
            }
        }

        protected override void BeginProcessing()
        {
            this._dataCollectionProfile = new AzurePSDataCollectionProfile(false);

            if (PBIClientSession.Instance.Profile.Environments.Count == 0)
            {
                throw new PSInvalidOperationException(string.Format(Resources.NotLoggedInMessage, ""));
            }

            capacityName = Instance;
            Uri uriResult;

            // if the user specifies the FQN of the capacity, then extract the capacityname out of that.
            // and set the current context
            if (Uri.TryCreate(Instance, UriKind.Absolute, out uriResult) && uriResult.Scheme == "asazure")
            {
                capacityName = uriResult.PathAndQuery.Trim('/');
                if (string.Compare(PBIClientSession.Instance.Profile.Context.Environment.Name, uriResult.DnsSafeHost, StringComparison.InvariantCultureIgnoreCase) != 0)
                {
                    throw new PSInvalidOperationException(string.Format(Resources.NotLoggedInMessage, Instance));
                }
            }
            else
            {
                var currentContext = PBIClientSession.Instance.Profile.Context;
                if (currentContext != null
                    && PBIClientSession.PBIRolloutEnvironmentMapping.ContainsKey(currentContext.Environment.Name))
                {
                    throw new PSInvalidOperationException(string.Format(Resources.InvalidCapacityName, capacityName));
                }
            }

            if (this.PBIHttpClient == null)
            {
                this.PBIHttpClient = new PBIHttpClient(() =>
                {
                    HttpClientHandler httpClientHandler = new HttpClientHandler();
                    httpClientHandler.AllowAutoRedirect = false;
                    return new HttpClient();
                });
            }

            if (this.TokenCacheItemProvider == null)
            {
                this.TokenCacheItemProvider = new TokenCacheItemProvider();
            }

            base.BeginProcessing();
        }

        protected override void InitializeQosEvent()
        {
            // No data collection for this commandlet
        }
        protected override string DataCollectionWarning
        {
            get
            {
                return Resources.ARMDataCollectionMessage;
            }
        }

        /// <summary>
        /// Worker Method for the synchronize request.
        /// </summary>
        /// <param name="context">The AS azure context</param>
        /// <param name="syncBaseUri">Base Uri for sync</param>
        /// <param name="databaseName">Database name</param>
        /// <param name="accessToken">Access token</param>
        /// <param name="maxNumberOfAttempts">Max number of retries for get command</param>
        /// <returns></returns>
        private async Task<ScaleOutCapacityDatabaseSyncDetails> SynchronizeDatabaseAsync(
            PBIContext context,
            Uri syncBaseUri,
            string databaseName,
            string accessToken)
        {
            Tuple<Uri, RetryConditionHeaderValue> pollingUrlAndRetryAfter = new Tuple<Uri, RetryConditionHeaderValue>(null, null);
            ScaleOutCapacityDatabaseSyncDetails syncResult = null;

            return await Task.Run(async () =>
            {
                try
                {
                    // pollingUrlAndRetryAfter = await PostSyncRequestAsync(context, syncBaseUri, databaseName, accessToken);
                    var synchronize = string.Format((string)context.Environment.Endpoints[PBIEnvironment.AsRolloutEndpoints.SyncEndpoint], this.clusterResolveResult.CoreCapacityName, databaseName);
                    this.PBIHttpClient.resetHttpClient();
                    using (var message = await PBIHttpClient.CallPostAsync(
                        syncBaseUri,
                        synchronize,
                        accessToken,
                        correlationId,
                        null))
                    {
                        this.syncRequestRootActivityId = message.Headers.Contains(RootActivityIdHeaderName) ? message.Headers.GetValues(RootActivityIdHeaderName).FirstOrDefault() : string.Empty;
                        this.syncRequestTimeStamp = message.Headers.Contains(CurrentUtcDateHeaderName) ? message.Headers.GetValues(CurrentUtcDateHeaderName).FirstOrDefault() : string.Empty;

                        message.EnsureSuccessStatusCode();

                        if (message.StatusCode != HttpStatusCode.Accepted)
                        {
                            var timestampNow = DateTime.Now;
                            syncResult = new ScaleOutCapacityDatabaseSyncDetails
                            {
                                CorrelationId = correlationId.ToString(),
                                Database = databaseName,
                                SyncState = DatabaseSyncState.Completed,
                                Details = string.Format("Http status code: {0}. Nothing readonly instances found to replicate databases.", message.StatusCode),
                                UpdatedAt = timestampNow,
                                StartedAt = timestampNow
                            };

                            return syncResult;
                        }

                        pollingUrlAndRetryAfter = new Tuple< Uri, RetryConditionHeaderValue>(message.Headers.Location, message.Headers.RetryAfter);
                    }

                }
                catch (Exception e)
                {
                    var timestampNow = DateTime.Now;

                    // Return sync details with exception message as details
                    return new ScaleOutCapacityDatabaseSyncDetails
                    {
                        CorrelationId = correlationId.ToString(),
                        Database = databaseName,
                        SyncState = DatabaseSyncState.Invalid,
                        Details = Resources.PostSyncRequestFailureMessage.FormatInvariant(
                                                                this.clusterResolveResult.CoreCapacityName,
                                                                this.syncRequestRootActivityId,
                                                                this.syncRequestTimeStamp,
                                                                string.Format(e.Message)),
                        UpdatedAt = timestampNow,
                        StartedAt = timestampNow
                    };
                }

                Uri pollingUrl = pollingUrlAndRetryAfter.Item1;
                var retryAfter = pollingUrlAndRetryAfter.Item2;

                try
                {
                    ScaleOutCapacityDatabaseSyncResult result = await this.PollSyncStatusWithRetryAsync(
                            databaseName,
                            accessToken,
                            pollingUrl,
                            retryAfter.Delta ?? DefaultPollingInterval);
                    syncResult = ScaleOutCapacityDatabaseSyncDetails.FromResult(result, correlationId.ToString());
                }
                catch (Exception e)
                {
                    var timestampNow = DateTime.Now;

                    // Append exception message to sync details and return
                    syncResult = new ScaleOutCapacityDatabaseSyncDetails
                    {
                        CorrelationId = correlationId.ToString(),
                        Database = databaseName,
                        SyncState = DatabaseSyncState.Invalid,
                        Details = Resources.SyncASPollStatusFailureMessage.FormatInvariant(
                                capacityName,
                                string.Empty,
                                timestampNow.ToString(CultureInfo.InvariantCulture),
                                string.Format(e.StackTrace)),
                        UpdatedAt = timestampNow,
                        StartedAt = timestampNow
                    };
                }

                return syncResult;
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="databaseName">Database name</param>
        /// <param name="accessToken">Access token</param>
        /// <param name="pollingUrl">URL for polling</param>
        /// <param name="pollingInterval">Polling interval set by the post response</param>
        /// <param name="maxNumberOfAttempts">Max number of attempts for each poll before the attempt is declared a failure</param>
        /// <returns></returns>
        private async Task<ScaleOutCapacityDatabaseSyncResult> PollSyncStatusWithRetryAsync(string databaseName, string accessToken, Uri pollingUrl, TimeSpan pollingInterval, int maxNumberOfAttempts = 3)
        {
            return await Task.Run(async () =>
            {
                ScaleOutCapacityDatabaseSyncResult response = null;
                var syncCompleted = false;
                do
                {
                    var retryCount = 0;
                    while (retryCount < maxNumberOfAttempts)
                    {
                        // Wait for specified polling interval other than retries.
                        if (retryCount == 0)
                        {
                            // WriteInformation(new InformationRecord(string.Format("Synchronize database {0}. Attempt #{1}. Waiting for {2} seconds to get sync results...", databaseName, retryCount, pollingInterval.TotalSeconds), string.Empty));
                            await Task.Delay(pollingInterval);
                        }
                        else
                        {
                            await Task.Delay(DefaultRetryIntervalForPolling);
                        }

                        this.PBIHttpClient.resetHttpClient();
                        using (HttpResponseMessage message = await PBIHttpClient.CallGetAsync(
                            pollingUrl,
                            string.Empty,
                            accessToken,
                            correlationId))
                        {
                            syncCompleted = !message.StatusCode.Equals(HttpStatusCode.SeeOther);
                            if (syncCompleted)
                            {
                                if (message.IsSuccessStatusCode)
                                {
                                    var responseString = await message.Content.ReadAsStringAsync();
                                    response = JsonConvert.DeserializeObject<ScaleOutCapacityDatabaseSyncResult>(responseString);
                                    break;
                                }
                                else
                                {
                                    retryCount++;
                                    if (response == null)
                                    {
                                        response = new ScaleOutCapacityDatabaseSyncResult()
                                        {
                                            Database = databaseName,
                                            SyncState = DatabaseSyncState.Invalid
                                        };

                                        response.Details = string.Format(
                                            "Http Error code: {0}. {1}", 
                                            message.StatusCode.ToString(), 
                                            message.Content != null ? await message.Content.ReadAsStringAsync() : string.Empty);
                                    }

                                    if (message.StatusCode >= (HttpStatusCode)400 && message.StatusCode <= (HttpStatusCode)499)
                                    {
                                        break;   
                                    }
                                }
                            }
                            else
                            {
                                pollingUrl = message.Headers.Location;
                                pollingInterval = message.Headers.RetryAfter.Delta ?? pollingInterval;
                            }
                        }
                    }
                }
                while (!syncCompleted);

                return response;
            });
        }

        /// <summary>
        /// Resolves the cluster to which the request needs to be sent for the current environment
        /// </summary>
        /// <param name="context"></param>
        /// <param name="capacityName"></param>
        /// <returns></returns>
        private ClusterResolutionResult ClusterResolve(PBIContext context, string capacityName)
        {
            Uri clusterResolveBaseUri = new Uri(string.Format("{0}{1}{2}", Uri.UriSchemeHttps, Uri.SchemeDelimiter, context.Environment.Name));
            UriBuilder resolvedUriBuilder = new UriBuilder(clusterResolveBaseUri);
            string rolloutAccessToken = this.TokenCacheItemProvider.GetTokenFromTokenCache(PBIClientSession.TokenCache, context.Account.UniqueId, context.Environment.Name);

            var resolveEndpoint = "/webapi/clusterResolve";
            var content = new StringContent($"CapacityName={capacityName}");
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");

            this.PBIHttpClient.resetHttpClient();
            using (HttpResponseMessage message = PBIHttpClient.CallPostAsync(
                clusterResolveBaseUri,
                resolveEndpoint,
                rolloutAccessToken,
                content).Result)
            {
                message.EnsureSuccessStatusCode();
                var rawResult = message.Content.ReadAsStringAsync().Result;
                ClusterResolutionResult result = JsonConvert.DeserializeObject<ClusterResolutionResult>(rawResult);
                return result;
            }
        }
    }
}
