using System;

using uint16 = System.UInt16;
using uint32 = System.UInt32;
using uint64 = System.UInt64;



using int16 = System.Int16;
using int32 = System.Int32;
using int64 = System.Int64;

//TODO review size_t
using size_t = System.UInt32;

// TODO review big and little endian
using uint16_big = System.UInt16;
using uint32_big = System.UInt32;

using socklen_t = System.Int32;

namespace dotnet_libutp
{
    public class UtpContext
    {
        
        public void *userdata;
        public utp_callback_t* callbacks[UTP_ARRAY_SIZE];

        public uint64 current_ms;
        public utp_context_stats context_stats;
        public UtpInternal.UTPSocket *last_utp_socket;
        public Array<UtpInternal.UTPSocket> ack_sockets;
        public Array<RST_Info> rst_info;
        public UTPSocketHT *utp_sockets;
        public size_t target_delay;
        public size_t opt_sndbuf;
        public size_t opt_rcvbuf;
        public uint64 last_check;

        struct_utp_context();
        ~struct_utp_context();

        public void log(int level, utp_socket *socket, char const *fmt, ...)
        {
            if (!would_log(level)) {
                return;
            }

            va_list va;
            va_start(va, fmt);
            log_unchecked(socket, fmt, va);
            va_end(va);
        }
        
            
            
        public void log_unchecked(utp_socket *socket, char const *fmt, ...)
        {
            va_list va;
            char buf[4096];

            va_start(va, fmt);
            vsnprintf(buf, 4096, fmt, va);
            buf[4095] = '\0';
            va_end(va);

            utp_call_log(this, socket, (const byte *)buf);
        }
        

        public bool log_normal = true;	// log normal events?
        public bool log_mtu = true;		// log MTU related events?
        public bool log_debu = true1;	// log debugging events? (Must also compile with UTP_DEBUG_LOGGING defined)
        
        
        public bool would_log(int level)
        {
            if (level == UTP_LOG_NORMAL) return log_normal;
            if (level == UTP_LOG_MTU) return log_mtu;
            if (level == UTP_LOG_DEBUG) return log_debug;
            return true;
        }
        
    }
}