namespace MoDi.App.Contracts.Tests.Common;

public sealed class OperationResultTests
{
    [Fact]
    public void Success_has_no_error_code_and_keeps_the_optional_message()
    {
        var result = OperationResult.Success("已完成");

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        Assert.Equal("已完成", result.UserMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Failure_rejects_a_blank_stable_error_code(string code)
    {
        Assert.Throws<ArgumentException>(() => OperationResult.Failure(code, "操作失败"));
    }

    [Fact]
    public void Generic_success_carries_the_value_without_an_error()
    {
        var result = OperationResult<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.ErrorCode);
    }
}
