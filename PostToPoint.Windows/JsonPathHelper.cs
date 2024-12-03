using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PostToPoint.Windows
{
    internal class JsonPathHelper
    {
        internal static string FindFirstValidPath(JsonElement element, JsonPathElement[] path, int pathIndex = 0)
        {
            if (pathIndex >= path.Length)
            {
                return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
            }

            var currentPathElement = path[pathIndex];

            if (!element.TryGetProperty(currentPathElement.PropertyName, out JsonElement nextElement))
                return null;

            if (nextElement.ValueKind == JsonValueKind.Null)
                return null;

            if (currentPathElement.IsList)
            {
                // For array elements, try each item until we find a valid path
                foreach (JsonElement arrayElement in nextElement.EnumerateArray())
                {
                    string result = FindFirstValidPath(arrayElement, path, pathIndex + 1);
                    if (result != null)
                        return result;
                }
                return null;
            }
            else
            {
                return FindFirstValidPath(nextElement, path, pathIndex + 1);
            }
        }
    }
}
