using cAlgo.API;

namespace cAlgo.Robots;

[Robot(AccessRights = AccessRights.None)]
public partial class {IdentityClass} : Robot
{
    protected override void OnStart()
    {
        // To learn more about cTrader Algo visit our Help Center:
        // https://help.ctrader.com/ctrader-algo/

        Print("Hello, cBot!");
    }

    protected override void OnTick()
    {
        // Handle price updates here
    }

    protected override void OnStop()
    {
        // Handle cBot stop here
    }
}