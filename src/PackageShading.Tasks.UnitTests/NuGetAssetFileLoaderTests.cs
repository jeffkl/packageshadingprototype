using Shouldly;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PackageShading.Tasks.UnitTests
{
    public class NuGetAssetFileLoaderTests : TestBase
    {
        [Fact]
        public void TransitiveDependenciesAreCorrectlyLoaded()
        {
            string projectAssetsFile = Path.Combine(TestDirectory, "project.assets.json");

            File.WriteAllText(
                projectAssetsFile,
                @"{
  ""version"": 3,
  ""targets"": {
    "".NETFramework,Version=v4.7.2"": {
      ""Microsoft.CodeCoverage/17.3.0"": {
        ""type"": ""package"",
        ""compile"": {
          ""lib/net45/Microsoft.VisualStudio.CodeCoverage.Shim.dll"": {}
        },
        ""runtime"": {
          ""lib/net45/Microsoft.VisualStudio.CodeCoverage.Shim.dll"": {}
        },
        ""build"": {
          ""build/netstandard1.0/Microsoft.CodeCoverage.props"": {},
          ""build/netstandard1.0/Microsoft.CodeCoverage.targets"": {}
        }
      },
      ""Microsoft.NET.Test.Sdk/17.3.0"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Microsoft.CodeCoverage"": ""17.3.0""
        },
        ""compile"": {
          ""lib/net45/_._"": {}
        },
        ""runtime"": {
          ""lib/net45/_._"": {}
        },
        ""build"": {
          ""build/net45/Microsoft.NET.Test.Sdk.props"": {},
          ""build/net45/Microsoft.NET.Test.Sdk.targets"": {}
        },
        ""buildMultiTargeting"": {
          ""buildMultiTargeting/Microsoft.NET.Test.Sdk.props"": {}
        }
      },
      ""Newtonsoft.Json/12.0.1"": {
        ""type"": ""package"",
        ""compile"": {
          ""lib/net45/Newtonsoft.Json.dll"": {
            ""related"": "".pdb;.xml""
          }
        },
        ""runtime"": {
          ""lib/net45/Newtonsoft.Json.dll"": {
            ""related"": "".pdb;.xml""
          }
        }
      },
      ""Newtonsoft.Json.Bson/1.0.2"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Newtonsoft.Json"": ""12.0.1""
        },
        ""compile"": {
          ""lib/net45/Newtonsoft.Json.Bson.dll"": {
            ""related"": "".pdb;.xml""
          }
        },
        ""runtime"": {
          ""lib/net45/Newtonsoft.Json.Bson.dll"": {
            ""related"": "".pdb;.xml""
          }
        }
      }
    },
    ""net6.0"": {
      ""Microsoft.CodeCoverage/17.3.0"": {
        ""type"": ""package"",
        ""compile"": {
          ""lib/netcoreapp1.0/Microsoft.VisualStudio.CodeCoverage.Shim.dll"": {}
        },
        ""runtime"": {
          ""lib/netcoreapp1.0/Microsoft.VisualStudio.CodeCoverage.Shim.dll"": {}
        },
        ""build"": {
          ""build/netstandard1.0/Microsoft.CodeCoverage.props"": {},
          ""build/netstandard1.0/Microsoft.CodeCoverage.targets"": {}
        }
      },
      ""Microsoft.NET.Test.Sdk/17.3.0"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Microsoft.CodeCoverage"": ""17.3.0"",
          ""Microsoft.TestPlatform.TestHost"": ""17.3.0""
        },
        ""compile"": {
          ""lib/netcoreapp2.1/_._"": {}
        },
        ""runtime"": {
          ""lib/netcoreapp2.1/_._"": {}
        },
        ""build"": {
          ""build/netcoreapp2.1/Microsoft.NET.Test.Sdk.props"": {},
          ""build/netcoreapp2.1/Microsoft.NET.Test.Sdk.targets"": {}
        },
        ""buildMultiTargeting"": {
          ""buildMultiTargeting/Microsoft.NET.Test.Sdk.props"": {}
        }
      },
      ""Microsoft.TestPlatform.ObjectModel/17.3.0"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""NuGet.Frameworks"": ""5.11.0"",
          ""System.Reflection.Metadata"": ""1.6.0""
        },
        ""compile"": {
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.CoreUtilities.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.PlatformAbstractions.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.VisualStudio.TestPlatform.ObjectModel.dll"": {}
        },
        ""runtime"": {
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.CoreUtilities.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.PlatformAbstractions.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.VisualStudio.TestPlatform.ObjectModel.dll"": {}
        }
      },
      ""Microsoft.TestPlatform.TestHost/17.3.0"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Microsoft.TestPlatform.ObjectModel"": ""17.3.0"",
          ""Newtonsoft.Json"": ""9.0.1""
        },
        ""compile"": {
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.CommunicationUtilities.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.CoreUtilities.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.CrossPlatEngine.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.PlatformAbstractions.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.Utilities.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.VisualStudio.TestPlatform.Common.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.VisualStudio.TestPlatform.ObjectModel.dll"": {},
          ""lib/netcoreapp2.1/testhost.dll"": {
            ""related"": "".deps.json""
          }
        },
        ""runtime"": {
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.CommunicationUtilities.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.CoreUtilities.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.CrossPlatEngine.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.PlatformAbstractions.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.TestPlatform.Utilities.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.VisualStudio.TestPlatform.Common.dll"": {},
          ""lib/netcoreapp2.1/Microsoft.VisualStudio.TestPlatform.ObjectModel.dll"": {},
          ""lib/netcoreapp2.1/testhost.dll"": {
            ""related"": "".deps.json""
          }
        },
        ""build"": {
          ""build/netcoreapp2.1/Microsoft.TestPlatform.TestHost.props"": {}
        }
      },
      ""Newtonsoft.Json/12.0.1"": {
        ""type"": ""package"",
        ""compile"": {
          ""lib/netstandard2.0/Newtonsoft.Json.dll"": {
            ""related"": "".pdb;.xml""
          }
        },
        ""runtime"": {
          ""lib/netstandard2.0/Newtonsoft.Json.dll"": {
            ""related"": "".pdb;.xml""
          }
        }
      },
      ""Newtonsoft.Json.Bson/1.0.2"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Newtonsoft.Json"": ""12.0.1""
        },
        ""compile"": {
          ""lib/netstandard2.0/Newtonsoft.Json.Bson.dll"": {
            ""related"": "".pdb;.xml""
          }
        },
        ""runtime"": {
          ""lib/netstandard2.0/Newtonsoft.Json.Bson.dll"": {
            ""related"": "".pdb;.xml""
          }
        }
      },
      ""NuGet.Frameworks/5.11.0"": {
        ""type"": ""package"",
        ""compile"": {
          ""lib/netstandard2.0/NuGet.Frameworks.dll"": {
            ""related"": "".xml""
          }
        },
        ""runtime"": {
          ""lib/netstandard2.0/NuGet.Frameworks.dll"": {
            ""related"": "".xml""
          }
        }
      },
      ""System.Reflection.Metadata/1.6.0"": {
        ""type"": ""package"",
        ""compile"": {
          ""lib/netstandard2.0/System.Reflection.Metadata.dll"": {
            ""related"": "".xml""
          }
        },
        ""runtime"": {
          ""lib/netstandard2.0/System.Reflection.Metadata.dll"": {
            ""related"": "".xml""
          }
        }
      }
    }
  }
}");

            INuGetAssetFileLoader nuGetAssetFileLoader = new NuGetAssetFileLoader();

            Dictionary<string, Dictionary<PackageIdentity, HashSet<PackageIdentity>>> assetsFile = nuGetAssetFileLoader.LoadAssetsFile(projectAssetsFile);

            assetsFile.Keys.ShouldBe(new[]
            {
                ".NETFramework,Version=v4.7.2",
                "net6.0",
            });

            PackageIdentity packageMicrosoftCodeCoverage = new PackageIdentity("Microsoft.CodeCoverage", "17.3.0");
            PackageIdentity packageMicrosoftNETTestSdk = new PackageIdentity("Microsoft.NET.Test.Sdk", "17.3.0");
            PackageIdentity packageMicrosoftTestPlatformObjectModel = new PackageIdentity("Microsoft.TestPlatform.ObjectModel", "17.3.0");
            PackageIdentity packageMicrosoftTestPlatformTestHost = new PackageIdentity("Microsoft.TestPlatform.TestHost", "17.3.0");
            PackageIdentity packageNewtonsoftJson12 = new PackageIdentity("Newtonsoft.Json", "12.0.1");
            PackageIdentity packageNewtonsoftJson9 = new PackageIdentity("Newtonsoft.Json", "9.0.1");
            PackageIdentity packageNewtonsoftJsonBson = new PackageIdentity("Newtonsoft.Json.Bson", "1.0.2");
            PackageIdentity packageNuGetFrameworks = new PackageIdentity("NuGet.Frameworks", "5.11.0");
            PackageIdentity packageSystemReflectionMetadata = new PackageIdentity("System.Reflection.Metadata", "1.6.0");

            Dictionary<PackageIdentity, HashSet<PackageIdentity>> net472Packages = assetsFile[".NETFramework,Version=v4.7.2"];

            net472Packages.Keys.ShouldBe(new[]
            {
                packageMicrosoftCodeCoverage,
                packageMicrosoftNETTestSdk,
                packageNewtonsoftJson12,
                packageNewtonsoftJsonBson
            });

            net472Packages[packageMicrosoftCodeCoverage].ShouldBe(new HashSet<PackageIdentity>());

            net472Packages[packageMicrosoftNETTestSdk].ShouldBe(new HashSet<PackageIdentity>
            {
                packageMicrosoftCodeCoverage
            });

            net472Packages[packageNewtonsoftJson12].ShouldBe(new HashSet<PackageIdentity>());

            net472Packages[packageNewtonsoftJsonBson].ShouldBe(new HashSet<PackageIdentity>
            {
                packageNewtonsoftJson12
            });

            Dictionary<PackageIdentity, HashSet<PackageIdentity>> net60Packages = assetsFile["net6.0"];

            net60Packages.Keys.ShouldBe(new[]
            {
                packageMicrosoftCodeCoverage,
                packageMicrosoftNETTestSdk,
                packageMicrosoftTestPlatformObjectModel,
                packageMicrosoftTestPlatformTestHost,
                packageNewtonsoftJson12,
                packageNewtonsoftJsonBson,
                packageNuGetFrameworks,
                packageSystemReflectionMetadata
            });

            net60Packages[packageMicrosoftCodeCoverage].ShouldBe(new HashSet<PackageIdentity>());

            net60Packages[packageMicrosoftNETTestSdk].ShouldBe(new HashSet<PackageIdentity>
            {
                packageMicrosoftCodeCoverage,
                packageMicrosoftTestPlatformTestHost,
                packageMicrosoftTestPlatformObjectModel,
                packageNewtonsoftJson9,
                packageNuGetFrameworks,
                packageSystemReflectionMetadata
            });

            net60Packages[packageMicrosoftTestPlatformObjectModel].ShouldBe(new HashSet<PackageIdentity>
            {
                packageNuGetFrameworks,
                packageSystemReflectionMetadata
            });

            net60Packages[packageMicrosoftTestPlatformTestHost].ShouldBe(new HashSet<PackageIdentity>
            {
                packageMicrosoftTestPlatformObjectModel,
                packageNewtonsoftJson9,
                packageNuGetFrameworks,
                packageSystemReflectionMetadata
            });

            net60Packages[packageNewtonsoftJson12].ShouldBe(new HashSet<PackageIdentity>());

            net60Packages[packageNewtonsoftJsonBson].ShouldBe(new HashSet<PackageIdentity>
            {
                packageNewtonsoftJson12
            });

            net60Packages[packageNuGetFrameworks].ShouldBe(new HashSet<PackageIdentity>());

            net60Packages[packageSystemReflectionMetadata].ShouldBe(new HashSet<PackageIdentity>());
        }
    }
}