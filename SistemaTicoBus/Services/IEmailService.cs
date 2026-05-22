namespace SistemaTicoBus.Web.Services
{
    public interface IEmailService
    {
        Task EnviarCorreoAsync(string destinatario, string asunto, string cuerpo);
    }
}