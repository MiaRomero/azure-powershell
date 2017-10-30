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

namespace Microsoft.Azure.Commands.PowerBIEmbeddedCapacity.Dataplane
{
    [Serializable]
    public partial class PBIAccount
    {
        public string Id { get; set; }

        public string UniqueId { get; set; }

        public string Tenant { get; set; }

        public string Type { get; set; }

        public string CertificateThumbprint { get; set; }

        /// <summary>
        /// string constants for known credential types
        /// </summary>
        public static class AccountType
        {
            public const string User = "User",
            ServicePrincipal = "ServicePrincipal";
        }
    }
}
