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
using Microsoft.Azure.Commands.PowerBIEmbeddedCapacity.Dataplane.Properties;

namespace Microsoft.Azure.Commands.PowerBIEmbeddedCapacity.Dataplane.Models
{
    public class SynchronizationFailedException : Exception
    {
        public SynchronizationFailedException()
            : base(Resources.SynchronizationFailedException)
        {
        }

        public SynchronizationFailedException(Exception e)
            : base(Resources.SynchronizationFailedException, e)
        {
        }

        public SynchronizationFailedException(string messageDetails)
            : base(messageDetails)
        {
        }

    }
}
