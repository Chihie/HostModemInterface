using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HostModemInterface
{
    public enum CommandEnum
    {
        RESET,
        SET_DL,
        SET_PHY,
        DL_DATA_REQUEST,
        PHY_DATA_REQUEST
    }
}
