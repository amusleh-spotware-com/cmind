import clr

clr.AddReference("cAlgo.API")

# Import cAlgo API types
from cAlgo.API import *

# Import trading wrapper functions
from robot_wrapper import *

class {IdentityClass}():
    def on_start(self):
        print(api.Message)
        # To learn more about cTrader Algo visit our Help Center:
        # https://help.ctrader.com/ctrader-algo/

    def on_tick(self):
        # Handle price updates here
        pass

    def on_stop(self):
        # Handle cBot stop here
        pass