using EunSlip.Core.Sending;
using MimeKit;
using MimeKit.Text;

namespace EunSlip.Infrastructure.Gmail;

public sealed class MimeMessageBuilder : IMimeMessageBuilder
{
    public MimeMessage Build(SendRequest request)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress(request.SenderDisplayName, "me"));
        message.To.Add(MailboxAddress.Parse(request.ToEmail));
        message.Subject = request.Subject;

        TextPart body = new(TextFormat.Plain) { Text = request.Body };

        byte[] attachmentBytes = File.ReadAllBytes(request.AttachmentPath);
        MimePart attachment = new("application", "pdf")
        {
            Content = new MimeContent(new MemoryStream(attachmentBytes)),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = request.AttachmentFileName,
        };

        Multipart multipart = new("mixed") { body, attachment };
        message.Body = multipart;
        return message;
    }
}

public interface IMimeMessageBuilder
{
    MimeMessage Build(SendRequest request);
}
