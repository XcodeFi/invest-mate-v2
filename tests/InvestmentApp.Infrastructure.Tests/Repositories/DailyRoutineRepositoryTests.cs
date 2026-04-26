using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Repositories;
using MongoDB.Driver;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Repositories;

/// <summary>
/// Regression guard for the Mongo index race condition triggered by the constructor's
/// legacy DropOne+CreateOne sequence. Two concurrent requests on a cold-started
/// container could race and produce
/// <c>MongoCommandException: Index build failed ... caused by dropIndexes command</c>.
///
/// The fix removes the legacy DropOne. These tests lock that behavior in so a future
/// edit re-introducing DropOne would fail loudly here, not on prod logs.
/// </summary>
public class DailyRoutineRepositoryTests
{
    private readonly Mock<IMongoIndexManager<DailyRoutine>> _indexManager = new();
    private readonly Mock<IMongoCollection<DailyRoutine>> _collection = new();
    private readonly Mock<IMongoDatabase> _database = new();

    public DailyRoutineRepositoryTests()
    {
        _collection.Setup(c => c.Indexes).Returns(_indexManager.Object);
        _database.Setup(d => d.GetCollection<DailyRoutine>("daily_routines", null))
            .Returns(_collection.Object);
    }

    [Fact]
    public void Constructor_DoesNotCallDropOne_AvoidingIndexBuildRace()
    {
        _ = new DailyRoutineRepository(_database.Object);

        // Critical regression guard: a previous version called DropOne("UserId_1_Date_1")
        // to migrate away from the old unique index. That migration is long done in
        // every environment, and the leftover DropOne raced with concurrent CreateOne
        // calls on cold start. Bringing it back would re-introduce the prod 500.
        _indexManager.Verify(
            i => i.DropOne(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "DropOne is dead migration code — re-introducing it caused MongoCommandException on prod.");
    }

    [Fact]
    public void Constructor_CreatesTwoIndexes_CompoundAndUserId()
    {
        _ = new DailyRoutineRepository(_database.Object);

        // Both indexes must still be created after the DropOne removal — they back
        // the GetByUserIdAndDateAsync + GetByUserIdRangeAsync queries.
        _indexManager.Verify(
            i => i.CreateOne(
                It.IsAny<CreateIndexModel<DailyRoutine>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
