using System.Text;
using FilipinaMorena.Api.Data;
using FilipinaMorena.Api.Models;
using FilipinaMorena.Api.Options;
using FilipinaMorena.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<MongoDbOptions>()
    .Bind(builder.Configuration.GetSection(MongoDbOptions.SectionName))
    .Validate(x => !string.IsNullOrWhiteSpace(x.ConnectionString), "MongoDB connection string is required.")
    .ValidateOnStart();
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(x => Encoding.UTF8.GetByteCount(x.SigningKey) >= 32, "JWT signing key must be at least 32 bytes.")
    .ValidateOnStart();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
var auth = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = jwt.Issuer,
    ValidateAudience = true,
    ValidAudience = jwt.Audience,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
    ClockSkew = TimeSpan.FromMinutes(1)
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
});

var googleId = builder.Configuration["Authentication:Google:ClientId"];
var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret))
    auth.AddGoogle(options => { options.ClientId = googleId; options.ClientSecret = googleSecret; options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme; });

var facebookId = builder.Configuration["Authentication:Facebook:AppId"];
var facebookSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
if (!string.IsNullOrWhiteSpace(facebookId) && !string.IsNullOrWhiteSpace(facebookSecret))
    auth.AddFacebook(options => { options.AppId = facebookId; options.AppSecret = facebookSecret; options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme; options.Scope.Add("email"); });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<TotpService>();

var app = builder.Build();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

await app.Services.GetRequiredService<MongoContext>().EnsureIndexesAsync();
app.Run();

public partial class Program;
