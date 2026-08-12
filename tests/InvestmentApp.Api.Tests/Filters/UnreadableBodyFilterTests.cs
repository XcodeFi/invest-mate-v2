using FluentAssertions;
using InvestmentApp.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace InvestmentApp.Api.Tests.Filters;

/// <summary>
/// `SuppressModelStateInvalidFilter = true` tắt 400 tự động vì UserId/Id là non-nullable mà client
/// không gửi. Hệ quả không ai muốn: JSON không đọc được thì tham số [FromBody] về null, action
/// deref nó và trả 500 "Object reference not set" — không nói được sai ở đâu.
///
/// Shape của ModelState dưới đây là shape THẬT, ghi lại từ một request curl chạy qua MVC:
/// khoá là JSON path (`$.ScenarioNodes[0].ActionType`), message là chuỗi của System.Text.Json,
/// và `Exception` là null. Bản đầu của filter dò theo `Exception` nên không bắt được gì — test
/// khi đó vẫn xanh vì nó tự bịa ra một ModelState mà MVC không bao giờ dựng.
/// </summary>
public class UnreadableBodyFilterTests
{
    private const string StjMessage =
        "The JSON value could not be converted to System.Nullable`1[InvestmentApp.Domain.Entities.ScenarioActionType]. "
        + "Path: $.ScenarioNodes[0].ActionType | LineNumber: 5 | BytePositionInLine: 73.";

    private static ActionExecutingContext Context(Action<ModelStateDictionary> arrange)
    {
        var modelState = new ModelStateDictionary();
        arrange(modelState);

        var actionContext = new ActionContext(
            new DefaultHttpContext(), new RouteData(), new ControllerActionDescriptor(), modelState);

        return new ActionExecutingContext(
            actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: null!);
    }

    [Fact]
    public void Unreadable_Json_Becomes_400_Naming_The_Path()
    {
        var context = Context(ms =>
        {
            // MVC ghi CẢ HAI khi body không đọc được: một lỗi theo JSON path, và một lỗi
            // "field is required" cho chính tham số — thứ tự này cũng là thứ tự thật.
            ms.AddModelError("command", "The command field is required.");
            ms.AddModelError("$.ScenarioNodes[0].ActionType", StjMessage);
        });

        new UnreadableBodyFilter().OnActionExecuting(context);

        var result = context.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var details = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        details.Status.Should().Be(400);
        details.Detail.Should().Contain("$.ScenarioNodes[0].ActionType");
    }

    [Fact]
    public void Missing_Server_Assigned_Field_Is_Left_Alone()
    {
        // UserId/Id do controller gán từ JWT/route sau khi bind. Đây là lý do
        // SuppressModelStateInvalidFilter tồn tại — filter không được phá nó.
        var context = Context(ms =>
        {
            ms.AddModelError("UserId", "The UserId field is required.");
            ms.AddModelError("Id", "The Id field is required.");
        });

        new UnreadableBodyFilter().OnActionExecuting(context);

        context.Result.Should().BeNull("thiếu field máy chủ tự gán không phải lỗi của client");
    }

    [Fact]
    public void Body_Argument_Required_Alone_Is_Left_Alone()
    {
        // Chỉ có "command field is required" mà KHÔNG có lỗi JSON path: không phải ca body hỏng.
        var context = Context(ms => ms.AddModelError("command", "The command field is required."));

        new UnreadableBodyFilter().OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void Valid_Request_Is_Left_Alone()
    {
        var context = Context(_ => { });

        new UnreadableBodyFilter().OnActionExecuting(context);

        context.Result.Should().BeNull();
    }
}
