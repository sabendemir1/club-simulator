public class VelvetClubRuntime
{
    public int level { get; set; } = 0;
    public int NofCustomers { get; set; } = 0;
    public double BarRevenue { get; set; } = 0;
    public double CloakRoomRevenue { get; set; } = 0;
    public double ShowRevenue { get; set; } = 0;
    public double LapBoothRevenue { get; set; } = 0;
    public double VipRoomRevenue { get; set; } = 0;
    public double EntranceRevenue { get; set; } = 0;
    public double TotalDailyRevenue { get; set; } = 0;
    public int totalBalance { get; set; } = 1000;
    public int followersCount { get; set; } = 5;
    public void newDay()
    {
        NofCustomers = 0;
        BarRevenue = 0;
        CloakRoomRevenue = 0;
        ShowRevenue = 0;
        LapBoothRevenue = 0;
        VipRoomRevenue = 0;
        EntranceRevenue = 0;
        TotalDailyRevenue = 0;
    }
}