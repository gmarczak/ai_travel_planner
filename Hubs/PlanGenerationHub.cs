using Microsoft.AspNetCore.SignalR;

namespace project.Hubs;

public class PlanGenerationHub : Hub
{
    public async Task JoinPlanGeneration(string planId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"plan_{planId}");
    }

    public async Task LeavePlanGeneration(string planId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"plan_{planId}");
    }
}
