using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MapRoutingApi.Services;
using MapRoutingApi.Models;
using MapRoutingApi.Authentication;

namespace MapRoutingApi.Authentication
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ApiKeyAuthAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string _requiredRole; // "FS_Read" or "FS_ReadWrite"

        public ApiKeyAuthAttribute(string requiredRole)
        {
            _requiredRole = requiredRole;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey))
            {
                context.Result = new UnauthorizedObjectResult("API Key missing");
                return;
            }

            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            // Simple validation: Check against appsettings
            // We expect appsettings to have: "ApiKeys": { "Key_Value": "Role" }
            var apiKeySection = configuration.GetSection("ApiKeys");
            var keyRole = apiKeySection[extractedApiKey.ToString()];

            if (string.IsNullOrEmpty(keyRole))
            {
                context.Result = new UnauthorizedObjectResult("Invalid API Key");
                return;
            }

            // Check permissions
            // Rule: FS_ReadWrite can do everything. FS_Read can only do Read.
            if (_requiredRole == "FS_ReadWrite" && keyRole != "FS_ReadWrite")
            {
                context.Result = new UnauthorizedObjectResult("Insufficient permissions");
                return;
            }

            // If required is FS_Read, then either FS_Read or FS_ReadWrite is fine.
        }
    }
}

namespace MapRoutingApi.Controllers
{
    [Route("api/map")]
    [ApiController]
    public class MapController : ControllerBase
    {
        private readonly IMapService _mapService;

        public MapController(IMapService mapService)
        {
            _mapService = mapService;
        }

        [HttpPost("SetMap")]
        [ApiKeyAuth("FS_ReadWrite")]
        public IActionResult SetMap([FromBody] List<Node> graph)
        {
            if (graph == null || graph.Count == 0)
            {
                return BadRequest("Invalid graph data.");
            }

            _mapService.SetMap(graph);
            return Ok();
        }

        [HttpGet("GetMap")]
        [ApiKeyAuth("FS_Read")]
        public IActionResult GetMap()
        {
            var map = _mapService.GetMap();
            if (map == null || map.Count == 0)
            {
                // This could be 200 [] or 400 Bad Request as per brief "if the map has not been set".
                // Brief says "400 Bad Request ... if the map has not been set"
                return BadRequest("Map has not been set.");
            }
            return Ok(map);
        }

        [HttpGet("ShortestRoute")]
        [ApiKeyAuth("FS_Read")]
        public IActionResult GetShortestRoute(string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                return BadRequest("Parameters 'from' and 'to' are required.");
            }

            var (path, distance) = _mapService.GetShortestPath(from, to);

            if (distance == -1) // Node not found or no path
            {
                // Is this 400 or 404? Brief says "400 Bad Request ... unknown node names"
                return BadRequest("Unknown node names or no path found.");
            }

            return Ok(string.Join("", path));
        }

        [HttpGet("ShortestDistance")]
        [ApiKeyAuth("FS_Read")]
        public IActionResult GetShortestDistance(string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                return BadRequest("Parameters 'from' and 'to' are required.");
            }

            var (path, distance) = _mapService.GetShortestPath(from, to);

            if (distance == -1)
            {
                return BadRequest("Unknown node names or no path found.");
            }

            return Ok(distance);
        }
    }
}
