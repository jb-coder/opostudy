using System.Text;

using OpoStudy.Mobile.Core.Auth;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Auth;

public sealed class GoogleServicesConfigTests
{
    private const string Valid = """
        {
          "project_info": { "project_id": "fake-project" },
          "client": [
            {
              "client_info": { "android_client_info": { "package_name": "com.other.app" } },
              "oauth_client": [ { "client_id": "999-other.apps.googleusercontent.com", "client_type": 3 } ],
              "api_key": [ { "current_key": "other-api-key" } ]
            },
            {
              "client_info": { "android_client_info": { "package_name": "com.opostudy.app" } },
              "oauth_client": [
                { "client_id": "123-android.apps.googleusercontent.com", "client_type": 1 },
                { "client_id": "123-web.apps.googleusercontent.com", "client_type": 3 }
              ],
              "api_key": [ { "current_key": "fake-api-key" } ]
            }
          ]
        }
        """;

    private static MemoryStream Stream(string json) => new(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Parse_ValidFile_ReturnsOptionsOfMatchingClient()
    {
        var options = GoogleServicesConfig.Parse(Stream(Valid), "com.opostudy.app");

        options.ApiKey.ShouldBe("fake-api-key");
        options.GoogleWebClientId.ShouldBe("123-web.apps.googleusercontent.com");
        options.ProjectId.ShouldBe("fake-project");
        options.IsConfigured.ShouldBeTrue();
    }

    [Fact]
    public void Parse_OtherPackageName_Throws()
    {
        var ex = Should.Throw<InvalidOperationException>(() => GoogleServicesConfig.Parse(Stream(Valid), "com.unknown.app"));

        ex.Message.ShouldContain("com.unknown.app");
    }

    [Fact]
    public void Parse_NoWebClient_ThrowsWithHelpfulMessage()
    {
        var json = Valid.Replace("\"client_id\": \"123-web.apps.googleusercontent.com\", \"client_type\": 3", "\"client_id\": \"123-web.apps.googleusercontent.com\", \"client_type\": 1", StringComparison.Ordinal);

        var ex = Should.Throw<InvalidOperationException>(() => GoogleServicesConfig.Parse(Stream(json), "com.opostudy.app"));

        ex.Message.ShouldContain("cliente web de OAuth");
    }

    [Fact]
    public void Parse_NoApiKey_Throws()
    {
        var json = Valid.Replace("\"current_key\": \"fake-api-key\"", "\"other\": \"x\"", StringComparison.Ordinal);

        var ex = Should.Throw<InvalidOperationException>(() => GoogleServicesConfig.Parse(Stream(json), "com.opostudy.app"));

        ex.Message.ShouldContain("current_key");
    }
}