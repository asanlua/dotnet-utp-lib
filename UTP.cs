namespace dotnet_libutp
{
    public class UTP
    {
        public enum UTP_OPTIONS {
            // callback names
            UTP_ON_FIREWALL = 0,
            UTP_ON_ACCEPT,
            UTP_ON_CONNECT,
            UTP_ON_ERROR,
            UTP_ON_READ,
            UTP_ON_OVERHEAD_STATISTICS,
            UTP_ON_STATE_CHANGE,
            UTP_GET_READ_BUFFER_SIZE,
            UTP_ON_DELAY_SAMPLE,
            UTP_GET_UDP_MTU,
            UTP_GET_UDP_OVERHEAD,
            UTP_GET_MILLISECONDS,
            UTP_GET_MICROSECONDS,
            UTP_GET_RANDOM,
            UTP_LOG,
            UTP_SENDTO,

            // context and socket options that may be set/queried
            UTP_LOG_NORMAL,
            UTP_LOG_MTU,
            UTP_LOG_DEBUG,
            UTP_SNDBUF,
            UTP_RCVBUF,
            UTP_TARGET_DELAY,

            UTP_ARRAY_SIZE,	// must be last
        };
    }
}