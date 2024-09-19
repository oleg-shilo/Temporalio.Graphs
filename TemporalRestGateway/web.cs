//css_webapp
//css_ng dotnet
//css_include global-usings

using Microsoft.AspNetCore.Http;

var app = WebApplication.Create(args);

app.Urls.Add("http://localhost:3200");
app.Urls.Add("https://localhost:4200");

app.MapGet("/", () => "Hello World!");

app.Run();