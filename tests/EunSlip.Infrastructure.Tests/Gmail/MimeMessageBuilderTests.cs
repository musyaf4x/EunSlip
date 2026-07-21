using EunSlip.Core.Sending;
using EunSlip.Infrastructure.Gmail;
using MimeKit;

namespace EunSlip.Infrastructure.Tests.Gmail;

public sealed class MimeMessageBuilderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eunslip-tests", Guid.NewGuid().ToString("N"));
    private readonly string _attachmentPath;

    public MimeMessageBuilderTests()
    {
        Directory.CreateDirectory(_directory);
        _attachmentPath = Path.Combine(_directory, "Slip_Gaji_JULY_2025_NIK0001.pdf");
        File.WriteAllBytes(_attachmentPath, [1, 2, 3, 4]);
    }

    private SendRequest Request(string attachmentName = "Slip_Gaji.pdf") => new(
        "budi@example.com", "Slip Gaji Karyawan", "Yth. Bapak/Ibu,\n\nTerlampir slip gaji.",
        _attachmentPath, attachmentName, "PT. EUNSUNG INDONESIA");

    [Fact]
    public void Build_HasExactlyOneToRecipient()
    {
        MimeMessage message = new MimeMessageBuilder().Build(Request());

        Assert.Single(message.To);
        Assert.Empty(message.Cc);
        Assert.Empty(message.Bcc);
    }

    [Fact]
    public void Build_SenderDisplayNameFromRequest()
    {
        MimeMessage message = new MimeMessageBuilder().Build(Request());

        Assert.Equal("PT. EUNSUNG INDONESIA", message.From[0].Name);
    }

    [Fact]
    public void Build_HasExactlyOnePdfAttachment()
    {
        MimeMessage message = new MimeMessageBuilder().Build(Request());

        List<MimePart> attachments = [.. message.Attachments.OfType<MimePart>()];
        MimePart attachment = Assert.Single(attachments);
        Assert.Equal("application/pdf", attachment.ContentType.MimeType);
        Assert.Equal("Slip_Gaji.pdf", attachment.FileName);
    }

    [Fact]
    public void Build_BodyIsPlainText()
    {
        MimeMessage message = new MimeMessageBuilder().Build(Request());

        TextPart? body = message.BodyParts.OfType<TextPart>().FirstOrDefault(p => !p.IsAttachment);
        Assert.NotNull(body);
        Assert.Equal("plain", body.ContentType.MediaSubtype);
        Assert.Contains("Terlampir slip gaji", body.Text);
    }

    [Fact]
    public void Build_SubjectFromRequest()
    {
        MimeMessage message = new MimeMessageBuilder().Build(Request());

        Assert.Equal("Slip Gaji Karyawan", message.Subject);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
