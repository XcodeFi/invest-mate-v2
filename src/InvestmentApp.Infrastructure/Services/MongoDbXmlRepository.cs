using Microsoft.AspNetCore.DataProtection.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System.Xml.Linq;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Persists ASP.NET Core Data Protection keys in MongoDB so they survive
/// container restarts and Cloud Run deployments.
/// </summary>
public class MongoDbXmlRepository : IXmlRepository
{
    private readonly IMongoCollection<DataProtectionKeyDocument> _collection;

    public MongoDbXmlRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<DataProtectionKeyDocument>("data_protection_keys");
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        return _collection.Find(FilterDefinition<DataProtectionKeyDocument>.Empty)
            .ToList()
            .Select(doc => XElement.Parse(doc.Xml))
            .ToList()
            .AsReadOnly();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        var doc = new DataProtectionKeyDocument
        {
            FriendlyName = friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting)
        };
        _collection.InsertOne(doc);
    }

    private class DataProtectionKeyDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
        public string FriendlyName { get; set; } = string.Empty;
        public string Xml { get; set; } = string.Empty;
    }
}
