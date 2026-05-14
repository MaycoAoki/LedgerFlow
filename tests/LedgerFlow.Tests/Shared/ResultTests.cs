using FluentAssertions;
using LedgerFlow.Shared.Result;

namespace LedgerFlow.Tests.Shared;

public class ResultTests
{
    [Fact]
    public void Ok_ShouldReturnSuccessResult()
    {
        var result = Result.Ok();

        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Fail_ShouldReturnFailureResult()
    {
        var result = Result.Fail(new Error("code", "message"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "code");
    }

    [Fact]
    public void ResultOfT_Ok_ShouldReturnSuccessWithValue()
    {
        var result = Result<string>.Ok("test");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ResultOfT_Fail_ShouldReturnFailureWithErrors()
    {
        var result = Result<string>.Fail(new Error("code", "message"));

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Errors.Should().Contain(e => e.Code == "code");
    }

    [Fact]
    public void ResultOfT_Fail_WithMultipleErrors_ShouldContainAllErrors()
    {
        var result = Result<string>.Fail(
            new Error("error1", "message1"),
            new Error("error2", "message2"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }
}
