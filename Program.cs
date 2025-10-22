using Prueba_SCISA_Michelle.Services;
using Prueba_SCISA_Michelle.Services.Abstractions;
using Prueba_SCISA_Michelle.Models.Options;

var builder = WebApplication.CreateBuilder(args);

// HttpClient hacia PokeAPI
builder.Services.AddHttpClient("pokeapi", c =>
{
    c.BaseAddress = new Uri("https://pokeapi.co/api/v2/");
    c.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddMemoryCache();

// MVC + API
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();

// Email config
builder.Services
    .AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection("Email"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "Email.ApiKey es requerido.")
    .ValidateOnStart();

// Servicios
builder.Services.AddScoped<IPokemonService, PokemonService>();
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// MVC (vistas)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Pokemon}/{action=Start}/{id?}");

// API (atributos)
app.MapControllers();


app.Run();
