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

namespace Microsoft.Azure.Commands.PowerBIEmbeddedCapacity.Test.ScenarioTests
{
    using Microsoft.WindowsAzure.Commands.ScenarioTest;
    using Xunit;

    public class PowerBIEmbeddedCapacityTests : PowerBIEmbeddedCapacityTestsBase
    {
        public PowerBIEmbeddedCapacityTests(Xunit.Abstractions.ITestOutputHelper output)
        {
            ServiceManagemenet.Common.Models.XunitTracingInterceptor.AddToContext(new ServiceManagemenet.Common.Models.XunitTracingInterceptor(output));
        }

        [Fact]
        [Trait(Category.AcceptanceType, Category.CheckIn)]
        public void TestPowerBIEmbeddedCapacity()
        {
            NewInstance.RunPsTest("Test-PowerBIEmbeddedCapacity");
        }

        [Fact]
        [Trait(Category.AcceptanceType, Category.CheckIn)]
        public void TestPowerBIEmbeddedCapacityScaleUpDown()
        {
            NewInstance.RunPsTest("Test-PowerBIEmbeddedCapacityScaleUpDown");
        }

        [Fact]
        [Trait(Category.AcceptanceType, Category.CheckIn)]
        public void TestNegativePowerBIEmbeddedCapacity()
        {
            NewInstance.RunPsTest("Test-NegativePowerBIEmbeddedCapacity");
        }

        [Fact]
        [Trait(Category.ServiceManagement, Category.LiveOnly)]
        public void TestPowerBIEmbeddedCapacityLogExport()
        {
            NewInstance.RunPsTest("Test-PowerBIEmbeddedCapacityLogExport");
        }

        [Fact]
        [Trait(Category.ServiceManagement, Category.LiveOnly)]
        public void TestPowerBIEmbeddedCapacityRestart()
        {
            NewInstance.RunPsTest("Test-PowerBIEmbeddedCapacityRestart");
        }

        [Fact]
        [Trait(Category.ServiceManagement, Category.LiveOnly)]
        public void TestPowerBIEmbeddedCapacitySynchronizeSingle()
        {
            NewInstance.RunPsTest("Test-PowerBIEmbeddedCapacitySynchronizeSingle");
        }

        [Fact]
        [Trait(Category.ServiceManagement, Category.LiveOnly)]
        public void TestPowerBIEmbeddedCapacityLoginWithSPN()
        {
            NewInstance.RunPsTest("Test-PowerBIEmbeddedCapacityLoginWithSPN");
        }
    }
}
