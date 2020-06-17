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
    public class UtpCallbacks
    {
	    
	    public enum UTP_UDP_DONTFRAG{
			UTP_UDP_DONTFRAG = 2,	// Used to be a #define as UDP_IP_DONTFRAG
		};

		public enum UTP_STATE {
			// socket has reveived syn-ack (notification only for outgoing connection completion)
			// this implies writability
			UTP_STATE_CONNECT = 1,

			// socket is able to send more data
			UTP_STATE_WRITABLE = 2,

			// connection closed
			UTP_STATE_EOF = 3,

			// socket is being destroyed, meaning all data has been sent if possible.
			// it is not valid to refer to the socket after this state change occurs
			UTP_STATE_DESTROYING = 4,
		};

		extern const char *utp_state_names[];

		// Errors codes that can be passed to UTP_ON_ERROR callback
		enum UTP {
			UTP_ECONNREFUSED = 0,
			UTP_ECONNRESET,
			UTP_ETIMEDOUT,
		};

		extern const char *utp_error_code_names[];

		enum UTP_2 {
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

		extern const char *utp_callback_names[];

		public class utp_callback_arguments {
			public utp_context context;
			public utp_socket socket;
			public size_t len;
			public uint32 flags;
			public int callback_type;
			public const byte *buf;

			// union {
				public sockaddr address;
				public int send;
				public int sample_ms;
				public int error_code;
				public int state;
			//}

			// union {
			public socklen_t address_len;
			public int type;
			// }
		} 

		typedef uint64 utp_callback_t(utp_callback_arguments *);

		// Returned by utp_get_context_stats()
		typedef struct {
			uint32 _nraw_recv[5];	// total packets recieved less than 300/600/1200/MTU bytes fpr all connections (context-wide)
			uint32 _nraw_send[5];	// total packets sent     less than 300/600/1200/MTU bytes for all connections (context-wide)
		} utp_context_stats;

		// Returned by utp_get_stats()
		public class utp_socket_stats {
			public uint64 nbytes_recv;	// total bytes received
			public uint64 nbytes_xmit;	// total bytes transmitted
			public uint32 rexmit;		// retransmit counter
			public uint32 fastrexmit;	// fast retransmit counter
			public uint32 nxmit;		// transmit counter
			public uint32 nrecv;		// receive counter (total)
			public uint32 nduprecv;	// duplicate receive counter
			public uint32 mtu_guess;	// Best guess at MTU
		} 

		#define UTP_IOV_MAX 1024

		// For utp_writev, to writes data from multiple buffers
		struct utp_iovec {
			void *iov_base;
			size_t iov_len;
		};
	    
	    
	    
	    
	    
		public static int utp_call_on_firewall(utp_context ctx,  struct sockaddr *address, socklen_t address_len)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_ON_FIREWALL]) return 0;
			args.callback_type = UTP_ON_FIREWALL;
			args.context = ctx;
			args.socket = NULL;
			args.address = address;
			args.address_len = address_len;
			return (int)ctx.callbacks[UTP_ON_FIREWALL](&args);
		}

		public static void utp_call_on_accept(utp_context ctx, utp_socket socket, const struct sockaddr *address, socklen_t address_len)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_ON_ACCEPT]) return;
			args.callback_type = UTP_ON_ACCEPT;
			args.context = ctx;
			args.socket = socket;
			args.address = address;
			args.address_len = address_len;
			ctx.callbacks[UTP_ON_ACCEPT](&args);
		}

		public static void utp_call_on_connect(utp_context ctx, utp_socket socket)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_ON_CONNECT]) return;
			args.callback_type = UTP_ON_CONNECT;
			args.context = ctx;
			args.socket = socket;
			ctx.callbacks[UTP_ON_CONNECT](&args);
		}

		public static void utp_call_on_error(utp_context ctx, utp_socket socket, int error_code)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_ON_ERROR]) return;
			args.callback_type = UTP_ON_ERROR;
			args.context = ctx;
			args.socket = socket;
			args.error_code = error_code;
			ctx.callbacks[UTP_ON_ERROR](&args);
		}

		public static void utp_call_on_read(utp_context ctx, utp_socket socket, const byte *buf, size_t len)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_ON_READ]) return;
			args.callback_type = UTP_ON_READ;
			args.context = ctx;
			args.socket = socket;
			args.buf = buf;
			args.len = len;
			ctx.callbacks[UTP_ON_READ](&args);
		}

		public static void utp_call_on_overhead_statistics(utp_context ctx, utp_socket socket, int send, size_t len, int type)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_ON_OVERHEAD_STATISTICS]) return;
			args.callback_type = UTP_ON_OVERHEAD_STATISTICS;
			args.context = ctx;
			args.socket = socket;
			args.send = send;
			args.len = len;
			args.type = type;
			ctx.callbacks[UTP_ON_OVERHEAD_STATISTICS](&args);
		}

		public static void utp_call_on_delay_sample(utp_context ctx, utp_socket socket, int sample_ms)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_ON_DELAY_SAMPLE]) return;
			args.callback_type = UTP_ON_DELAY_SAMPLE;
			args.context = ctx;
			args.socket = socket;
			args.sample_ms = sample_ms;
			ctx.callbacks[UTP_ON_DELAY_SAMPLE](&args);
		}

		public static void utp_call_on_state_change(utp_context ctx, utp_socket socket, int state)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_ON_STATE_CHANGE]) return;
			args.callback_type = UTP_ON_STATE_CHANGE;
			args.context = ctx;
			args.socket = socket;
			args.state = state;
			ctx.callbacks[UTP_ON_STATE_CHANGE](&args);
		}

		public static uint16 utp_call_get_udp_mtu(utp_context ctx, utp_socket socket, sockaddr address, socklen_t address_len)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_GET_UDP_MTU]) return 0;
			args.callback_type = UTP_GET_UDP_MTU;
			args.context = ctx;
			args.socket = socket;
			args.address = address;
			args.address_len = address_len;
			return (uint16)ctx.callbacks[UTP_GET_UDP_MTU](&args);
		}

		public static uint16 utp_call_get_udp_overhead(utp_context ctx, utp_socket socket, const struct sockaddr *address, socklen_t address_len)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_GET_UDP_OVERHEAD]) return 0;
			args.callback_type = UTP_GET_UDP_OVERHEAD;
			args.context = ctx;
			args.socket = socket;
			args.address = address;
			args.address_len = address_len;
			return (uint16)ctx.callbacks[UTP_GET_UDP_OVERHEAD](&args);
		}

		public static uint64 utp_call_get_milliseconds(utp_context ctx, utp_socket socket)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_GET_MILLISECONDS]) return 0;
			args.callback_type = UTP_GET_MILLISECONDS;
			args.context = ctx;
			args.socket = socket;
			return ctx.callbacks[UTP_GET_MILLISECONDS](&args);
		}

		public static uint64 utp_call_get_microseconds(utp_context ctx, utp_socket socket)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_GET_MICROSECONDS]) return 0;
			args.callback_type = UTP_GET_MICROSECONDS;
			args.context = ctx;
			args.socket = socket;
			return ctx.callbacks[UTP_GET_MICROSECONDS](&args);
		}

		public static uint32 utp_call_get_random(utp_context ctx, utp_socket socket)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_GET_RANDOM]) return 0;
			args.callback_type = UTP_GET_RANDOM;
			args.context = ctx;
			args.socket = socket;
			return (uint32)ctx.callbacks[UTP_GET_RANDOM](&args);
		}

		public static size_t utp_call_get_read_buffer_size(utp_context ctx, utp_socket socket)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_GET_READ_BUFFER_SIZE]) return 0;
			args.callback_type = UTP_GET_READ_BUFFER_SIZE;
			args.context = ctx;
			args.socket = socket;
			return (size_t)ctx.callbacks[UTP_GET_READ_BUFFER_SIZE](&args);
		}

		public static void utp_call_log(utp_context ctx, utp_socket socket, const byte *buf)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_LOG]) return;
			args.callback_type = UTP_LOG;
			args.context = ctx;
			args.socket = socket;
			args.buf = buf;
			ctx.callbacks[UTP_LOG](&args);
		}

		public static void utp_call_sendto(utp_context ctx, utp_socket socket, const byte *buf, size_t len, const struct sockaddr *address, socklen_t address_len, uint32 flags)
		{
			utp_callback_arguments args = new utp_callback_arguments();
			if (!ctx.callbacks[UTP_SENDTO]) return;
			args.callback_type = UTP_SENDTO;
			args.context = ctx;
			args.socket = socket;
			args.buf = buf;
			args.len = len;
			args.address = address;
			args.address_len = address_len;
			args.flags = flags;
			ctx.callbacks[UTP_SENDTO](&args);
		}
    }
}