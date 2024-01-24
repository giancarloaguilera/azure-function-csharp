using System.Collections.Specialized;
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

        var users = FilterUsers(PopulateUsers(), query)
                        .OrderBy(u => u.first_name)
                        .ThenBy(u => u.last_name)
                        .Take(take)
                        .ToList();

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

        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, config);

        csv.Read(); // skip header row

        var users = new List<User>();

        while (csv.Read())
        {
            users.Add(new User(
                csv.GetField<int>(0),
                csv.GetField(1)!,
                csv.GetField(2)!,
                csv.GetField(3)!,
                csv.GetField(4)!,
                csv.GetField(5)!,
                csv.GetField(6)!,
                csv.GetField(7)!,
                csv.GetField(8)!
            ));
        }

        return users;
    }

    public static IReadOnlyList<User> FilterUsers(IEnumerable<User> users, NameValueCollection query)
    {
        var firstname = query["firstname"];
        var lastname = query["lastname"];

        return users
            .Where(x => (string.IsNullOrEmpty(firstname) ||
                        x.first_name.StartsWith(firstname, StringComparison.OrdinalIgnoreCase))
                        && 
                        (string.IsNullOrEmpty(lastname) ||
                        x.last_name.StartsWith(lastname, StringComparison.OrdinalIgnoreCase))
                        )
            .ToList();
    }
}
