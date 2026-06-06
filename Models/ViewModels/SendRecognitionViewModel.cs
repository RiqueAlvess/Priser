using System.ComponentModel.DataAnnotations;

namespace Priser.Models.ViewModels;

public class SendRecognitionViewModel
{
    [Required(ErrorMessage = "Selecione um colaborador")]
    public Guid ReceiverId { get; set; }

    public Guid? CompanyValueId { get; set; }

    [Required(ErrorMessage = "Escreva uma mensagem")]
    [MinLength(10, ErrorMessage = "Mínimo 10 caracteres")]
    [MaxLength(500, ErrorMessage = "Máximo 500 caracteres")]
    public string Message { get; set; } = "";

    [Range(1, 10000, ErrorMessage = "Pontos entre 1 e 10000")]
    public int Points { get; set; } = 50;

    public string Visibility { get; set; } = "public";
}
