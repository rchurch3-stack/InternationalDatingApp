using FilipinaMorena.Api.Models;
using FilipinaMorena.Api.Options;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FilipinaMorena.Api.Data;

public sealed class MongoContext
{
    public IMongoCollection<AppUser> Users { get; }
    public IMongoCollection<ExternalLoginState> ExternalLoginStates { get; }

    public MongoContext(IOptions<MongoDbOptions> options)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        var database = client.GetDatabase(options.Value.DatabaseName);
        Users = database.GetCollection<AppUser>("users");
        ExternalLoginStates = database.GetCollection<ExternalLoginState>("externalLoginStates");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Users.Indexes.CreateOneAsync(
            new CreateIndexModel<AppUser>(
                Builders<AppUser>.IndexKeys.Ascending(x => x.NormalizedEmail),
                new CreateIndexOptions { Unique = true, Name = "ux_users_normalized_email" }),
            cancellationToken: cancellationToken);

        await ExternalLoginStates.Indexes.CreateOneAsync(
            new CreateIndexModel<ExternalLoginState>(
                Builders<ExternalLoginState>.IndexKeys.Ascending(x => x.ExpiresUtc),
                new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "ttl_external_login_states" }),
            cancellationToken: cancellationToken);
    }
}
