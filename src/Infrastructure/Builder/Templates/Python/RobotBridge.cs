using System.Globalization;
using Python.Runtime;

namespace cAlgo.{AlgoType};

internal class RobotBridge : BaseBridge
{
    private readonly SafeExecuteMethodProxy _onStartProxy;
    private readonly SafeExecuteMethodProxy _onTickProxy;
    private readonly SafeExecuteMethodProxy _onStopProxy;
    private readonly SafeExecuteMethodProxy _onBarProxy;
    private readonly SafeExecuteMethodProxy _onBarClosedProxy;
    private readonly SafeExecuteMethodWithResultProxy<double> _getFitnessProxy;
    private readonly SafeExecuteMethodProxy _onErrorProxy;

    internal RobotBridge(PyObject objectInstance)
        : base(objectInstance)
    {
        var numberFormatInfo = new NumberFormatInfo();

        _onStartProxy = new SafeExecuteMethodProxy(objectInstance, "on_start");
        _onTickProxy = new SafeExecuteMethodProxy(objectInstance, "on_tick");
        _onStopProxy = new SafeExecuteMethodProxy(objectInstance, "on_stop");
        _onBarProxy = new SafeExecuteMethodProxy(objectInstance, "on_bar");
        _getFitnessProxy = new SafeExecuteMethodWithResultProxy<double>(objectInstance, "get_fitness", pr => pr.ToDouble(numberFormatInfo), _ => 0);
        _onBarClosedProxy = new SafeExecuteMethodProxy(objectInstance, "on_bar_closed");
        _onErrorProxy = new SafeExecuteMethodProxy(objectInstance, "on_error");
    }

    internal void OnStart()
    {
        _onStartProxy.Invoke();
    }

    internal void OnTick()
    {
        _onTickProxy.Invoke();
    }

    internal void OnStop()
    {
        _onStopProxy.Invoke();
    }

    internal void OnBar()
    {
        _onBarProxy.Invoke();
    }

    internal void OnBarClosed()
    {
        _onBarClosedProxy.Invoke();
    }
    
    internal double GetFitness(GetFitnessArgs args)
    {
        return _getFitnessProxy.Invoke(args);
    }
    
    internal void OnError(Error error)
    {
        _onErrorProxy.Invoke(error);
    }
}