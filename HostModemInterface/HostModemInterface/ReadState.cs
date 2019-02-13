using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HostModemInterface
{
    public enum ReadState
    {
        BEGIN,
        LENGTH,
        CC,
        DATA,
        CHECKSUM_1,
        CHECKSUM_2
    }
}
