namespace Cyzor.Provisioning.Services;

public interface ISwaggerService
{
    string GetSwaggerUI();
}

public class SwaggerService : ISwaggerService
{
    public string GetSwaggerUI()
    {
            var defaultKey = Environment.GetEnvironmentVariable("PROVISIONING_API_KEY") ?? "test-key-12345";

            var part1 = @"<!DOCTYPE html>
<html>
<head>
    <title>Cyzor Provisioning API</title>
    <meta charset='utf-8'/>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/swagger-ui-dist/swagger-ui.css'>
    <style>
        html {
            box-sizing: border-box;
            overflow: -moz-scrollbars-vertical;
            overflow-y: scroll;
        }
        *, *:before, *:after {
            box-sizing: inherit;
        }
        body {
            margin: 0;
            padding: 0;
        }
    </style>
</head>
<body>
    <div id='swagger-ui'></div>
    <script src='https://cdn.jsdelivr.net/npm/swagger-ui-dist/swagger-ui-bundle.js'></script>
    <script src='https://cdn.jsdelivr.net/npm/swagger-ui-dist/swagger-ui-standalone-preset.js'></script>
    <script>
    (function(){
        // Store default API key in localStorage so the Authorize/requests use it
        try {
            localStorage.setItem('apiKey', '";

            var part2 = @"');
        } catch(e){}

        window.onload = function() {
            window.ui = SwaggerUIBundle({
                url: '/openapi.json',
                dom_id: '#swagger-ui',
                presets: [
                    SwaggerUIBundle.presets.apis,
                    SwaggerUIStandalonePreset
                ],
                layout: 'BaseLayout',
                requestInterceptor: (request) => {
                    request.headers['X-API-Key'] = localStorage.getItem('apiKey') || 'your-api-key-here';
                    return request;
                }
            });
        }
    })();
    </script>
</body>
</html>";

            return part1 + defaultKey + part2;
    }
}
