using System.Text.RegularExpressions;

namespace DIH.Common.Services.Database.Helpers
{
    /// <summary>
    ///     A utility class that allows the conversion of string expressions to Blob Index Tag / Cosmos DB query formats.
    ///     
    ///     Example constructor inputs:
    ///     
    ///      1. "field1='a' AND field2<>'b'"
    ///      2. "age > 21 AND isStudent=true"
    ///      3. "price >= 50 OR category='electronics'"
    ///      
    ///     URL query string conversion supported via the static method <see cref="FromQueryString" />
    /// </summary>
    internal static class QueryConverter
    {
        /// <summary>
        /// Converts the initial query to a format suitable for Azure Blob Index Tag search.
        /// </summary>
        /// <returns>A string formatted for Azure Blob Index Tag search.</returns>
        public static string ForBlobIndexTag(string query)
        {
            var matches = Regex.Matches(query, @"(?<field>\w+)\s*(?<operator>=|<>|<|>|<=|>=|AND|OR)\s*(?<value>\w+|'[^']+')|\s*(?<logical>AND|OR)\s*");
            List<string> results = new List<string>();

            foreach (Match match in matches)
            {
                if (match.Groups["logical"].Success)
                {
                    results.Add(MapOperator(match.Groups["logical"].Value, false));
                }
                else
                {
                    var field = ProcessFieldName(match.Groups["field"].Value, false);
                    var op = MapOperator(match.Groups["operator"].Value, false);
                    var value = ProcessValue(match.Groups["value"].Value, true);

                    results.Add($"{field} {op} {value}");
                }
            }

            return string.Join(" ", results);
        }

        /// <summary>
        /// Converts the initial query to a format suitable for Cosmos DB SQL.
        /// </summary>
        /// <returns>A string formatted for Cosmos DB SQL.</returns>
        public static string ForCosmosDB(string query)
        {
            var matches = Regex.Matches(query, @"(?<field>\w+)\s*(?<operator>=|<>|<|>|<=|>=|AND|OR)\s*(?<value>\w+|'[^']+')|\s*(?<logical>AND|OR)\s*");
            List<string> results = new List<string>();

            foreach (Match match in matches)
            {
                if (match.Groups["logical"].Success)
                {
                    results.Add(MapOperator(match.Groups["logical"].Value, true));
                }
                else
                {
                    var field = ProcessFieldName(match.Groups["field"].Value, true);
                    var op = MapOperator(match.Groups["operator"].Value, true);
                    var value = ProcessValue(match.Groups["value"].Value, false);

                    results.Add($"{field} {op} {value}");
                }
            }

            return string.Join(" ", results);
        }

        /// <summary>
        /// Parses a URL-style query string and returns is in Cosmos DB style.
        /// </summary>
        /// <param name="queryString">The URL-style query string to parse.</param>
        /// <returns>Cosmos DB version of the query</returns>
        public static string ForCosmosDBFromQueryString(string queryString)
        {
            List<string> queries = GetQUeriesFromUrlStyle(queryString);
            return ForCosmosDB(string.Join(" AND ", queries));
        }

        /// <summary>
        /// Parses a URL-style query string and returns is in Blob Index Tag style.
        /// </summary>
        /// <param name="queryString">The URL-style query string to parse.</param>
        /// <returns>Blob Index Tag version of the query</returns>
        public static string ForBlobIndexTagFromQueryString(string queryString)
        {
            List<string> queries = GetQUeriesFromUrlStyle(queryString);
            return ForBlobIndexTag(string.Join(" AND ", queries));
        }

        private static List<string> GetQUeriesFromUrlStyle(string queryString)
        {
            var pairs = queryString.Split('&');
            List<string> queries = new List<string>();
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0];
                    var value = keyValue[1];

                    if (int.TryParse(value, out _) || bool.TryParse(value, out _))
                    {
                        queries.Add($"{key}={value}");
                    }
                    else
                    {
                        var escapedValue = value.Replace("'", "\\'");
                        queries.Add($"{key}='{escapedValue}'");
                    }
                }
            }

            return queries;
        }

        private static string ProcessValue(string value, bool forBlob)
        {
            value = value.Trim();

            if (int.TryParse(value, out _))
            {
                return forBlob ? $"'{value}'" : value;
            }

            if (bool.TryParse(value, out _))
            {
                return forBlob ? $"'{value}'" : value.ToLower();
            }

            // Ensure it's enclosed in single quotes for strings
            return value.StartsWith("'") ? value : $"'{value}'";
        }

        private static string ProcessFieldName(string fieldName, bool forCosmos)
        {
            return forCosmos ? $"c.{fieldName}" : $"\"{fieldName}\"";
        }

        private static string MapOperator(string op, bool forCosmos)
        {
            switch (op)
            {
                case "=":
                    return forCosmos ? " = " : op;
                case "<>":
                    return forCosmos ? " != " : op;
                case ">":
                    return forCosmos ? " > " : op;
                case ">=":
                    return forCosmos ? " >= " : op;
                case "<":
                    return forCosmos ? " < " : op;
                case "<=":
                    return forCosmos ? " <= " : op;
                case "AND":
                    return forCosmos ? " AND " : op;
                case "OR":
                    return forCosmos ? " OR " : op;
                default:
                    return op;
            }
        }
    }
}

