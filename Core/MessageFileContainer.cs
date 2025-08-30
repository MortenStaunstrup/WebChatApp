namespace Core;

public class MessageFileContainer
{
    public string FileName { get; set; }
    public int SenderId { get; set; }
    public byte[] File {get;set;}
}