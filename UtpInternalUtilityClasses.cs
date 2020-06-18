using System;

using uint16 = System.UInt16;
using uint32 = System.UInt32;
using uint64 = System.UInt64;

using utp_context = dotnet_libutp.UtpContext;


using int16 = System.Int16;
using int32 = System.Int32;
using int64 = System.Int64;

//TODO review size_t
using size_t = System.UInt32;

using utp_socket = dotnet_libutp.UtpInternal.UTPSocket;

// TODO review big and little endian
using uint16_big = System.UInt16;
using uint32_big = System.UInt32;

using socklen_t = System.Int32;

namespace dotnet_libutp
{
    public class UtpInternalUtilityClasses
    {
        public class  UTPSocketKeyData {
            public UTPSocketKey key;
            public dotnet_libutp.UtpInternal.UTPSocket socket;
            public utp_link_t link;
        };
    }
    
    public class UTPSocketKey {
        public PackedSockAddr addr;
        public uint32 recv_id;		 // "conn_seed", "conn_id"

        public UTPSocketKey(PackedSockAddr& _addr, uint32 _recv_id) {
            memset(this, 0, sizeof(*this));
            addr = _addr;
            recv_id = _recv_id;
        }

        public bool operator == (UTPSocketKey &other) {
            return recv_id == other.recv_id && addr == other.addr;
        }

        uint32 compute_hash()  {
            return recv_id ^ addr.compute_hash();
        }
    };
}