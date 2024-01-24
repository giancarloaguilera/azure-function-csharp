using System.Globalization;
using System.Net;
using System.Reflection;
using System.Web;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace My.Functions;

public record User
(
    long id,
    string first_name,
    string last_name,
    string email,
    string department,
    string city,
    string state,
    string zip,
    string uuid
);

public class HttpExample
{
    private static readonly CsvConfiguration config = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = false,
    };

    public HttpExample(ILoggerFactory loggerFactory)
    {
        loggerFactory.CreateLogger<HttpExample>().LogInformation("Application started");
    }

    [Function("HttpExample")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);

        if (!int.TryParse(query["take"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int take) || take <= 0) {
            take = 10;
        }

        var users = FilterUsers(PopulateUsers(), take, query["firstname"]);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(users);

        return response;
    }

    public static IReadOnlyList<User> PopulateUsers()
    {
        var csvStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("azure_function_csharp.system_users.csv");

        if (csvStream is null)
        {
            throw new InvalidOperationException("No embedded CSV found");
        }

        using var csv = new CsvReader(new StreamReader(csvStream), config);

        return csv.GetRecords<User>().ToList();
    }

    public static IReadOnlyList<User> FilterUsers(IEnumerable<User> users, int take, string? firstname)
    {
        return users
            .Where(x => string.IsNullOrEmpty(firstname) ||
                        x.first_name.StartsWith(firstname, StringComparison.OrdinalIgnoreCase))
            .Take(take)
            .ToList();
    }
}
