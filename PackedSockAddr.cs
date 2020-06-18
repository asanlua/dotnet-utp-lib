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
    public class PackedSockAddr
    {
        
        // The values are always stored here in network byte order
        public class _in {
            byte[] _in6 = new byte[16];		// IPv6
            uint16[] _in6w = new uint16[8];	// IPv6, word based (for convenience)
            uint32[] _in6d = new uint32[4];	// Dword access
            in6_addr _in6addr;	// For convenience
        } //_in;

        // Host byte order
        uint16 _port;

        #define _sin4 _in._in6d[3]	// IPv4 is stored where it goes if mapped

        #define _sin6 _in._in6
        #define _sin6w _in._in6w
        #define _sin6d _in._in6d


        


        
        byte get_family() 
        {
            #if defined(__sh__)
	                return ((_sin6d[0] == 0) && (_sin6d[1] == 0) && (_sin6d[2] == htonl(0xffff)) != 0) ?
		                AF_INET : AF_INET6;
            #else
                            return (IN6_IS_ADDR_V4MAPPED(&_in._in6addr) != 0) ? AF_INET : AF_INET6;
            #endif // defined(__sh__)
        }
        
        
        
        public bool operator==(PackedSockAddr rhs) 
		{
			if (&rhs == this)
				return true;
			if (_port != rhs._port)
				return false;
			return memcmp(_sin6, rhs._sin6, sizeof(_sin6)) == 0;
		}

		public bool operator!=(PackedSockAddr rhs) 
		{
			return !(*this == rhs);
		}

		uint32 compute_hash()  {
			return utp_hash_mem(&_in, sizeof(_in)) ^ _port;
		}

		void set(SOCKADDR_STORAGE* sa, socklen_t len)
		{
			if (sa.ss_family == AF_INET) {
				assert(len >= sizeof(sockaddr_in));
				const sockaddr_in *sin = (sockaddr_in*)sa;
				_sin6w[0] = 0;
				_sin6w[1] = 0;
				_sin6w[2] = 0;
				_sin6w[3] = 0;
				_sin6w[4] = 0;
				_sin6w[5] = 0xffff;
				_sin4 = sin.sin_addr.s_addr;
				_port = ntohs(sin.sin_port);
			} else {
				assert(len >= sizeof(sockaddr_in6));
				const sockaddr_in6 *sin6 = (sockaddr_in6*)sa;
				_in._in6addr = sin6.sin6_addr;
				_port = ntohs(sin6.sin6_port);
			}
		}

		public PackedSockAddr(SOCKADDR_STORAGEΩ sa, socklen_t len)
		{
			set(sa, len);
		}

		public PackedSockAddr()
		{
			SOCKADDR_STORAGE sa;
			socklen_t len = sizeof(SOCKADDR_STORAGE);
			memset(&sa, 0, len);
			sa.ss_family = AF_INET;
			set(&sa, len);
		}

		public SOCKADDR_STORAGE get_sockaddr_storage(socklen_t *len = NULL) 
		{
			SOCKADDR_STORAGE sa;
			const byte family = get_family();
			if (family == AF_INET) {
				sockaddr_in *sin = (sockaddr_in*)&sa;
				if (len) *len = sizeof(sockaddr_in);
				memset(sin, 0, sizeof(sockaddr_in));
				sin.sin_family = family;
				sin.sin_port = htons(_port);
				sin.sin_addr.s_addr = _sin4;
			} else {
				sockaddr_in6 *sin6 = (sockaddr_in6*)&sa;
				memset(sin6, 0, sizeof(sockaddr_in6));
				if (len) *len = sizeof(sockaddr_in6);
				sin6.sin6_family = family;
				sin6.sin6_addr = _in._in6addr;
				sin6.sin6_port = htons(_port);
			}
			return sa;
		}

		// #define addrfmt(x, s) x.fmt(s, sizeof(s))
		cstr fmt(str s, size_t len)
		{
			memset(s, 0, len);
			byte family = get_family();
			str i;
			if (family == AF_INET) {
				INET_NTOP(family, (uint32*)&_sin4, s, len);
				i = s;
				while (*++i) {}
			} else {
				i = s;
				*i++ = '[';
				INET_NTOP(family, (in6_addr*)&_in._in6addr, i, len-1);
				while (*++i) {}
				*i++ = ']';
			}
			snprintf(i, len - (i-s), ":%u", _port);
			return s;
		}
            
            
        

    }
}