using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

public class InterviewHub : Hub
{
    public async Task SendTranscript(string text)
    {
        await Clients.All.SendAsync("ReceiveTranscript", text);
    }

    public async Task SendStatus(string status)
    {
        await Clients.All.SendAsync("ReceiveStatus", status);
    }

    public async Task SendQuestion(string question)
    {
        await Clients.All.SendAsync("ReceiveQuestion", question);
    }
}