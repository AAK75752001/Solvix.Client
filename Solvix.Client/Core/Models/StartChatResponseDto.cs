
namespace Solvix.Client.Core.Models
{
    public class StartChatResponseDto
    {
        public Guid ChatId { get; set; }
        public bool AlreadyExists { get; set; }
    }
}
