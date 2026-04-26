using FluentAssertions;
using InvestmentApp.Api.Auth;

namespace InvestmentApp.Api.Tests.Auth;

public class SchedulerEmailAllowlistTests
{
    [Fact]
    public void IsAllowed_EmptyConfig_RejectsAll()
    {
        var sut = new SchedulerEmailAllowlist("");

        sut.Count.Should().Be(0);
        sut.IsAllowed("any@iam.gserviceaccount.com").Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_NullConfig_RejectsAll()
    {
        var sut = new SchedulerEmailAllowlist(null);

        sut.Count.Should().Be(0);
        sut.IsAllowed("any@iam.gserviceaccount.com").Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_EmailInList_ReturnsTrue()
    {
        var sut = new SchedulerEmailAllowlist("scheduler@proj.iam.gserviceaccount.com");

        sut.IsAllowed("scheduler@proj.iam.gserviceaccount.com").Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_EmailNotInList_ReturnsFalse()
    {
        var sut = new SchedulerEmailAllowlist("a@x.com");

        sut.IsAllowed("b@x.com").Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_CaseInsensitive_EmailMatchesRegardlessOfCase()
    {
        var sut = new SchedulerEmailAllowlist("Scheduler@Proj.iam.gserviceaccount.com");

        sut.IsAllowed("scheduler@proj.iam.gserviceaccount.com").Should().BeTrue();
        sut.IsAllowed("SCHEDULER@PROJ.IAM.GSERVICEACCOUNT.COM").Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_MultipleEmails_AllAccepted()
    {
        var sut = new SchedulerEmailAllowlist("a@x.com,b@y.com,c@z.com");

        sut.Count.Should().Be(3);
        sut.IsAllowed("a@x.com").Should().BeTrue();
        sut.IsAllowed("b@y.com").Should().BeTrue();
        sut.IsAllowed("c@z.com").Should().BeTrue();
        sut.IsAllowed("d@w.com").Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_EmailsWithSurroundingWhitespace_AreTrimmed()
    {
        var sut = new SchedulerEmailAllowlist("  a@x.com , b@y.com  ");

        sut.Count.Should().Be(2);
        sut.IsAllowed("a@x.com").Should().BeTrue();
        sut.IsAllowed("b@y.com").Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_NullOrEmptyEmail_ReturnsFalse()
    {
        var sut = new SchedulerEmailAllowlist("a@x.com");

        sut.IsAllowed(null).Should().BeFalse();
        sut.IsAllowed("").Should().BeFalse();
    }
}
