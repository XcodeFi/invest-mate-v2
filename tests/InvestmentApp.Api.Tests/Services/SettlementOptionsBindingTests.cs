using FluentAssertions;
using InvestmentApp.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Api.Tests.Services;

/// <summary>
/// Ghim hợp đồng cấu hình chu kỳ thanh toán: KHÔNG có key thì T+2, CÓ key thì theo key.
/// Dựng lại đúng chuỗi đăng ký của <c>Program.cs</c> (Bind + ValidateDataAnnotations +
/// ValidateOnStart) chứ không gọi binder trần — nếu không thì test xanh trong khi app thật
/// vẫn có thể chưa nối dây.
/// </summary>
public class SettlementOptionsBindingTests
{
    private static IServiceProvider Build(params (string Key, string Value)[] settings)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s =>
                new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<SettlementOptions>()
            .Bind(config.GetSection(SettlementOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services.BuildServiceProvider();
    }

    private static int Sessions(IServiceProvider sp)
        => sp.GetRequiredService<IOptions<SettlementOptions>>().Value.Sessions;

    [Fact]
    public void Khong_co_key_nao_thi_giu_T2()
    {
        Sessions(Build()).Should().Be(2);
    }

    [Fact]
    public void Co_section_nhung_thieu_Sessions_thi_van_giu_T2()
    {
        // Bind không ghi gì lên property khi key vắng, nên initializer của object sống nguyên.
        Sessions(Build(("Settlement:GhiChu", "chỉ có ghi chú"))).Should().Be(2);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("3", 3)]
    public void Co_key_thi_theo_key(string configured, int expected)
    {
        Sessions(Build(("Settlement:Sessions", configured))).Should().Be(expected);
    }

    [Fact]
    public void Dat_0_la_T0_that_chu_khong_bi_coi_la_chua_cau_hinh()
    {
        // Bẫy kinh điển: `Sessions == 0 ? Default : Sessions` sẽ làm ca này ra 2 và
        // bịt hẳn đường đặt T+0 mà không có test nào khác đỏ.
        Sessions(Build(("Settlement:Sessions", "0"))).Should().Be(0);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("11")]
    public void Gia_tri_vo_nghia_chet_ngay_luc_doc_chu_khong_de_lai_so_tien_sai(string configured)
    {
        var sp = Build(("Settlement:Sessions", configured));

        var act = () => Sessions(sp);

        act.Should().Throw<OptionsValidationException>();
    }
}
