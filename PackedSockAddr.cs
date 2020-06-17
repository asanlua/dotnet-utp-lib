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
    public class UtpPackedSockAddr
    {
        class PackedSockAddr {
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

            byte get_family() const;
            bool operator==( PackedSockAddr& rhs) ;
            bool operator!=( PackedSockAddr& rhs) ;
            void set(const SOCKADDR_STORAGE* sa, socklen_t len);

            PackedSockAddr(SOCKADDR_STORAGE* sa, socklen_t len);
            PackedSockAddr(void);

            SOCKADDR_STORAGE get_sockaddr_storage(socklen_t *len) const;
            cstr fmt(str s, size_t len) const;

            uint32 compute_hash();
            
            byte get_family() 
            {
                #if defined(__sh__)
		                return ((_sin6d[0] == 0) && (_sin6d[1] == 0) && (_sin6d[2] == htonl(0xffff)) != 0) ?
			                AF_INET : AF_INET6;
                #else
                                return (IN6_IS_ADDR_V4MAPPED(&_in._in6addr) != 0) ? AF_INET : AF_INET6;
                #endif // defined(__sh__)
            }
        } //ALIGNED_ATTRIBUTE(4);

    }
}