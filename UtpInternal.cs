using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using uint16 = System.Int64;//System.UInt16;
using uint32 = System.Int64;//System.UInt32;
using uint64 = System.Int64;//System.UInt64;

using utp_context = dotnet_libutp.UtpContext;



using int16 = System.Int64;//System.Int16;
using int32 = System.Int64;//System.Int32;
using int64 = System.Int64;//System.Int64;

//TODO review size_t
using size_t = System.Int64;//System.UInt32;

using utp_socket = dotnet_libutp.UtpInternal.UTPSocket;

// TODO review big and little endian
using uint16_big = System.Int64;//System.UInt16;
using uint32_big = System.Int64;//System.UInt32;

using UTPSocketKeyData = dotnet_libutp.UtpInternalUtilityClasses.UTPSocketKeyData;
using UTPSocketKey = dotnet_libutp.UtpInternalUtilityClasses.UTPSocketKeyData;

using socklen_t = System.Int64;//System.Int32;
	
namespace dotnet_libutp
{
    public unsafe class UtpInternal
    {
	    
        public static int TIMEOUT_CHECK_INTERVAL = 500;
	        
        // number of bytes to increase max window size by, per RTT. This is
		// scaled down linearly proportional to off_target. i.e. if all packets
		// in one window have 0 delay, window size will increase by this number.
		// Typically it's less. TCP increases one MSS per RTT, which is 1500
		public static int MAX_CWND_INCREASE_BYTES_PER_RTT = 3000;

		public static int CUR_DELAY_SIZE = 3;
		
		// experiments suggest that a clock skew of 10 ms per 325 seconds
		// is not impossible. Reset delay_varBase every 13 minutes. The clock
		// skew is dealt with by observing the delay varBase in the other
		// direction, and adjusting our own upwards if the opposite direction
		// delay varBase keeps going down
		public static int DELAY_varBase_HISTORY = 13;
		public static int MAX_WINDOW_DECAY = 100; // ms

		public static int REORDER_BUFFER_SIZE = 32;
		public static int REORDER_BUFFER_MAX_SIZE = 1024;
		public static int OUTGOING_BUFFER_MAX_SIZE = 1024;

		public static int PACKET_SIZE = 1435;

		// this is the minimum max_window value. It can never drop below this
		public static int MIN_WINDOW_SIZE = 10;

		// if we receive 4 or more duplicate acks, we resend the packet
		// that hasn't been acked yet
		public static int DUPLICATE_ACKS_BEFORE_RESEND = 3;

		// Allow a reception window of at least 3 ack_nrs behind seq_nr
		// A non-SYN packet with an ack_nr difference greater than this is
		// considered suspicious and ignored
		public static string ACK_NR_ALLOWED_WINDOW = "DUPLICATE_ACKS_BEFORE_RESEND";

		public static int RST_INFO_TIMEOUT = 10000;
		public static int RST_INFO_LIMIT = 1000;
		// 29 seconds determined from measuring many home NAT devices
		public static int KEEPALIVE_INTERVAL = 29000;
		
		public static uint SEQ_NR_MASK = 0xFFFF;
		public static uint ACK_NR_MASK = 0xFFFF;
		public static uint TIMESTAMP_MASK = 0xFFFFFFFF;

		public static float DIV_ROUND_UP(float num, float denom)
		{
			return ((num + denom - 1) / denom);
		}

		// The totals are derived from the following data:
		//  45: IPv6 address including embedded IPv4 address
		//  11: Scope Id
		//   2: Brackets around IPv6 address when port is present
		//   6: Port (including colon)
		//   1: Terminating null byte
		public char[] addrbuf = new char[65];
		
		//TODO
		//#define addrfmt(x, s) x.fmt(s, sizeof(s))

		


		// these packet sizes are including the uTP header wich
		// is either 20 or 23 bytes depending on version
	    public static int PACKET_SIZE_EMPTY_BUCKET = 0;
	    public static int PACKET_SIZE_EMPTY = 23;
	    public static int PACKET_SIZE_SMALL_BUCKET = 1;
	    public static int PACKET_SIZE_SMALL = 373;
	    public static int PACKET_SIZE_MID_BUCKET = 2;
	    public static int PACKET_SIZE_MID = 723;
	    public static int PACKET_SIZE_BIG_BUCKET = 3;
	    public static int PACKET_SIZE_BIG = 1400;
	    public static int PACKET_SIZE_HUGE_BUCKET = 4;
	    
	    
	    public enum MAX_EACK { MAX_EACK = 128 };
	    
	    

	    public struct PacketFormatV1 {
			// packet_type (4 high bits)
			// protocol version (4 low bits)
			public int ver_type;

			public int version() { return ver_type & 0xf; }

			public int type() { return ver_type >> 4; }
			public void set_version(byte v) { ver_type = (ver_type & 0xf0) | (v & 0xf); }
			public void set_type(byte t) { ver_type = (ver_type & 0xf) | (t << 4); }

			// Type of the first extension header
			public byte ext;
			// connection ID
			public uint16_big connid;
			public uint32_big tv_usec;
			public uint32_big reply_micro;
			// receive window size in bytes
			public uint32_big windowsize;
			// Sequence number
			public uint16_big seq_nr;
			// Acknowledgment number
			public uint16_big ack_nr;
		};

	    public class PacketFormatAckV1 {
			public PacketFormatV1 pf;
			public byte ext_next;
			public byte ext_len;
			public byte[] acks = new byte[4];
		};

		

	    public enum ST {
			ST_DATA = 0,		// Data packet.
			ST_FIN = 1,			// Finalize the connection. This is the last packet.
			ST_STATE = 2,		// State packet. Used to transmit an ACK with no data.
			ST_RESET = 3,		// Terminate connection forcefully.
			ST_SYN = 4,			// Connect SYN
			ST_NUM_STATES,		// used for bounds checking
		};

	    public static string[]  flagnames = {
			"ST_DATA","ST_FIN","ST_STATE","ST_RESET","ST_SYN"
		};

	    public enum CONN_STATE {
			CS_UNINITIALIZED = 0,
			CS_IDLE,
			CS_SYN_SENT,
			CS_SYN_RECV,
			CS_CONNECTED,
			CS_CONNECTED_FULL,
			CS_RESET,
			CS_DESTROY
		};

	    public static string[]  statenames = {
			"UNINITIALIZED", "IDLE","SYN_SENT", "SYN_RECV", "CONNECTED","CONNECTED_FULL","DESTROY_DELAY","RESET","DESTROY"
		};

	    public class OutgoingPacket {
			public size_t length;
			public size_t payload;
			public uint64 time_sent; // microseconds
			public uint transmissions = 31;
			public bool need_resend = true;
			public byte[] data = new byte[1];
		};

	    public class SizableCircularBuffer {

		    // This is the mask. Since it's always a power of 2, adding 1 to this value will return the size.
			public size_t mask;
			// This is the elements that the circular buffer points to
			public List<Object> elements = new List<object>();

			public Object get(int i)
			{
				return elements == null ? elements.ElementAt(i) : null;
			}
			public void put(int i, Object data) {  elements.Insert(i, data); }
			
			public void ensure_size(size_t item, size_t index) { if (index > mask) grow(item, index); }
			public size_t size() { return mask + 1; }
			
			// Item contains the element we want to make space for
			// index is the index in the list.
			public void grow(size_t item, size_t index)
			{
				// // Figure out the new size.
				// size_t size = mask + 1;
				// do size *= 2; while (index >= size);
				//
				// // Allocate the new buffer
				// void **buf = (void**)calloc(size, sizeof(void*));
				//
				// size--;
				//
				// // Copy elements from the old buffer to the new buffer
				// for (size_t i = 0; i <= mask; i++) {
				// 	buf[(item - index + i) & size] = get(item - index + i);
				// }
				//
				// // Swap to the newly allocated buffer
				// mask = size;
				// elements = buf;
			}
			
		};



		// compare if lhs is less than rhs, taking wrapping
		// into account. if lhs is close to UINT_MAX and rhs
		// is close to 0, lhs is assumed to have wrapped and
		// considered smaller
		public static bool wrapping_compare_less(uint32 lhs, uint32 rhs, uint32 mask)
		{
			// distance walking from lhs to rhs, downwards
			uint32 dist_down = (lhs - rhs) & mask;
			// distance walking from lhs to rhs, upwards
			uint32 dist_up = (rhs - lhs) & mask;

			// if the distance walking up is shorter, lhs
			// is less than rhs. If the distance walking down
			// is shorter, then rhs is less than lhs
			return dist_up < dist_down;
		}

		public class DelayHist {
			public uint32 delay_varBase;

			// this is the history of delay samples,
			// normalized by using the delay_varBase. These
			// values are always greater than 0 and measures
			// the queuing delay in microseconds
			public uint32[] cur_delay_hist = new uint32[CUR_DELAY_SIZE];
			public size_t cur_delay_idx;

			// this is the history of delay_varBase. It's
			// a number that doesn't have an absolute meaning
			// only relative. It doesn't make sense to initialize
			// it to anything other than values relative to
			// what's been seen in the real world.
			public uint32[] delay_varBase_hist = new uint32[DELAY_varBase_HISTORY];
			public size_t delay_varBase_idx;
			// the time when we last stepped the delay_varBase_idx
			public uint64 delay_varBase_time;

			public bool delay_varBase_initialized;

			public void clear(uint64 current_ms)
			{
				delay_varBase_initialized = false;
				delay_varBase = 0;
				cur_delay_idx = 0;
				delay_varBase_idx = 0;
				delay_varBase_time = current_ms;
				for (size_t i = 0; i < CUR_DELAY_SIZE; i++) {
					cur_delay_hist[i] = 0;
				}
				for (size_t i = 0; i < DELAY_varBase_HISTORY; i++) {
					delay_varBase_hist[i] = 0;
				}
			}

			public void shift(uint32 offset)
			{
				// the offset should never be "negative"
				// //assert(offset < 0x10000000);

				// increase all of our varBase delays by this amount
				// this is used to take clock skew into account
				// by observing the other side's changes in its varBase_delay
				for (size_t i = 0; i < DELAY_varBase_HISTORY; i++) {
					delay_varBase_hist[i] += offset;
				}
				delay_varBase += offset;
			}

			public void add_sample(uint32 sample, uint64 current_ms)
			{
				// The two clocks (in the two peers) are assumed not to
				// progress at the exact same rate. They are assumed to be
				// drifting, which causes the delay samples to contain
				// a systematic error, either they are under-
				// estimated or over-estimated. This is why we update the
				// delay_varBase every two minutes, to adjust for this.

				// This means the values will keep drifting and eventually wrap.
				// We can cross the wrapping boundry in two directions, either
				// going up, crossing the highest value, or going down, crossing 0.

				// if the delay_varBase is close to the max value and sample actually
				// wrapped on the other end we would see something like this:
				// delay_varBase = 0xffffff00, sample = 0x00000400
				// sample - delay_varBase = 0x500 which is the correct difference

				// if the delay_varBase is instead close to 0, and we got an even lower
				// sample (that will eventually update the delay_varBase), we may see
				// something like this:
				// delay_varBase = 0x00000400, sample = 0xffffff00
				// sample - delay_varBase = 0xfffffb00
				// this needs to be interpreted as a negative number and the actual
				// recorded delay should be 0.

				// It is important that all arithmetic that assume wrapping
				// is done with unsigned intergers. Signed integers are not guaranteed
				// to wrap the way unsigned integers do. At least GCC takes advantage
				// of this relaxed rule and won't necessarily wrap signed ints.

				// remove the clock offset and propagation delay.
				// delay varBase is min of the sample and the current
				// delay varBase. This min-operation is subject to wrapping
				// and care needs to be taken to correctly choose the
				// true minimum.

				// specifically the problem case is when delay_varBase is very small
				// and sample is very large (because it wrapped past zero), sample
				// needs to be considered the smaller

				if (!delay_varBase_initialized) {
					// delay_varBase being 0 suggests that we haven't initialized
					// it or its history with any real measurements yet. Initialize
					// everything with this sample.
					for (size_t i = 0; i < DELAY_varBase_HISTORY; i++) {
						// if we don't have a value, set it to the current sample
						delay_varBase_hist[i] = sample;
						continue;
					}
					delay_varBase = sample;
					delay_varBase_initialized = true;
				}

				if (wrapping_compare_less(sample, delay_varBase_hist[delay_varBase_idx], TIMESTAMP_MASK)) {
					// sample is smaller than the current delay_varBase_hist entry
					// update it
					delay_varBase_hist[delay_varBase_idx] = sample;
				}

				// is sample lower than delay_varBase? If so, update delay_varBase
				if (wrapping_compare_less(sample, delay_varBase, TIMESTAMP_MASK)) {
					// sample is smaller than the current delay_varBase
					// update it
					delay_varBase = sample;
				}

				// this operation may wrap, and is supposed to
				uint32 delay = sample - delay_varBase;
				// sanity check. If this is triggered, something fishy is going on
				// it means the measured sample was greater than 32 seconds!
				////assert(delay < 0x2000000);

				cur_delay_hist[cur_delay_idx] = delay;
				cur_delay_idx = (cur_delay_idx + 1) % CUR_DELAY_SIZE;

				// once every minute
				if (current_ms - delay_varBase_time > 60 * 1000) {
					delay_varBase_time = current_ms;
					delay_varBase_idx = (delay_varBase_idx + 1) % DELAY_varBase_HISTORY;
					// clear up the new delay varBase history spot by initializing
					// it to the current sample, then update it
					delay_varBase_hist[delay_varBase_idx] = sample;
					delay_varBase = delay_varBase_hist[0];
					// Assign the lowest delay in the last 2 minutes to delay_varBase
					for (size_t i = 0; i < DELAY_varBase_HISTORY; i++) {
						if (wrapping_compare_less(delay_varBase_hist[i], delay_varBase, TIMESTAMP_MASK))
							delay_varBase = delay_varBase_hist[i];
					}
				}
			}

			uint32 get_value()
			{
				uint32 value = UInt32.MaxValue;
				for (size_t i = 0; i < CUR_DELAY_SIZE; i++) {
					//value = min<uint32>(cur_delay_hist[i], value);
					value = cur_delay_hist[i] > value ? cur_delay_hist[i] : value;
				}
				// value could be UINT_MAX if we have no samples yet...
				return value;
			}
		};



		public class UTPSocket {
			//~UTPSocket();
			~UTPSocket()
			{
				

				UtpCallbacks.utp_call_on_state_change(ctx, this, (int) UtpCallbacks.UTP_STATE.UTP_STATE_DESTROYING);

				if (ctx.last_utp_socket.Equals(this)) {
					ctx.last_utp_socket = null;
				}

				// Remove object from the global hash table
				UTPSocketKeyData kd = ctx.utp_sockets.Delete(UTPSocketKey(addr, conn_id_recv));
				//assert(kd);

				// remove the socket from ack_sockets if it was there also
				//removeSocketFromAckList(this);
				
			}

			public PackedSockAddr addr;
			public UtpContext ctx;

			public int ida; //for ack socket list

			public uint16 retransmit_count;

			public uint16 reorder_count;
			public byte duplicate_ack;

			// the number of packets in the send queue. Packets that haven't
			// yet been sent count as well as packets marked as needing resend
			// the oldest un-acked packet in the send queue is seq_nr - cur_window_packets
			public uint16 cur_window_packets;

			// how much of the window is used, number of bytes in-flight
			// packets that have not yet been sent do not count, packets
			// that are marked as needing to be re-sent (due to a timeout)
			// don't count either
			public size_t cur_window;
			// maximum window size, in bytes
			public size_t max_window;
			// UTP_SNDBUF setting, in bytes
			public size_t opt_sndbuf;
			// UTP_RCVBUF setting, in bytes
			public size_t opt_rcvbuf;

			// this is the target delay, in microseconds
			// for this socket. defaults to 100000.
			public size_t target_delay;

			// Is a FIN packet in the reassembly buffer?
			public bool got_fin = true;
			// Have we reached the FIN?
			public bool got_fin_reached = true;

			// Have we sent our FIN?
			public bool fin_sent = true;
			// Has our fin been ACKed?
			public bool fin_sent_acked = true;

			// Reading is disabled
			public bool read_shutdown = true;
			// User called utp_close()
			public bool close_requested = true;

			// Timeout procedure
			public bool fast_timeout = true;

			// max receive window for other end, in bytes
			public size_t max_window_user;
			public CONN_STATE state;
			// TickCount when we last decayed window (wraps)
			public int64 last_rwin_decay;

			// the sequence number of the FIN packet. This field is only set
			// when we have received a FIN, and the flag field has the FIN flag set.
			// it is used to know when it is safe to destroy the socket, we must have
			// received all packets up to this sequence number first.
			public uint16 eof_pkt;

			// All sequence numbers up to including this have been properly received
			// by us
			public uint16 ack_nr;
			// This is the sequence number for the next packet to be sent.
			public uint16 seq_nr;

			public uint16 timeout_seq_nr;

			// This is the sequence number of the next packet we're allowed to
			// do a fast resend with. This makes sure we only do a fast-resend
			// once per packet. We can resend the packet with this sequence number
			// or any later packet (with a higher sequence number).
			public uint16 fast_resend_seq_nr;

			public uint32 reply_micro;

			public uint64 last_got_packet;
			public uint64 last_sent_packet;
			public uint64 last_measured_delay;

			// timestamp of the last time the cwnd was full
			// this is used to prevent the congestion window
			// from growing when we're not sending at capacity
			public uint64 last_maxed_out_window;

			public void *userdata;

			// Round trip time
			public uint rtt;
			// Round trip time variance
			public uint rtt_var;
			// Round trip timeout
			public uint rto;
			public DelayHist rtt_hist;
			public uint retransmit_timeout;
			// The RTO timer will timeout here.
			public uint64 rto_timeout;
			// When the window size is set to zero, start this timer. It will send a new packet every 30secs.
			public uint64 zerowindow_time;

			public uint32 conn_seed;
			// Connection ID for packets I receive
			public uint32 conn_id_recv;
			// Connection ID for packets I send
			public uint32 conn_id_send;
			// Last rcv window we advertised, in bytes
			public size_t last_rcv_win;

			public DelayHist our_hist;
			public DelayHist their_hist;

			// extension bytes from SYN packet
			public byte[] extensions = new byte[8];

			// MTU Discovery
			// time when we should restart the MTU discovery
			public uint64 mtu_discover_time;
			// ceiling and floor of binary search. last is the mtu size
			// we're currently using
			public uint32 mtu_ceiling, mtu_floor, mtu_last;
			// we only ever have a single probe in flight at any given time.
			// this is the sequence number of that probe, and the size of
			// that packet
			public uint32 mtu_probe_seq, mtu_probe_size;

			// this is the average delay samples, as compared to the initial
			// sample. It's averaged over 5 seconds
			public int32 average_delay;
			// this is the sum of all the delay samples
			// we've made recently. The important distinction
			// of these samples is that they are all made compared
			// to the initial sample, this is to deal with
			// wrapping in a simple way.
			public int64 current_delay_sum;
			// number of sample ins current_delay_sum
			public int current_delay_samples;
			// initialized to 0, set to the first raw delay sample
			// each sample that's added to current_delay_sum
			// is subtracted from the value first, to make it
			// a delay relative to this sample
			public uint32 average_delay_varBase;
			// the next time we should add an average delay
			// sample into average_delay_hist
			public uint64 average_sample_time;
			// the estimated clock drift between our computer
			// and the endpoint computer. The unit is microseconds
			// per 5 seconds
			public int32 clock_drift;
			// just used for logging
			public int32 clock_drift_raw;

			public SizableCircularBuffer inbuf, outbuf;

			// #ifdef _DEBUG
			// // Public per-socket statistics, returned by utp_get_stats()
			// utp_socket_stats _stats;
			// #endif

			// true if we're in slow-start (exponential growth) phase
			public bool slow_start;

			// the slow-start threshold, in bytes
			public size_t ssthresh;

			

			
			
			public void schedule_ack()
			{
				if (ida == -1){
					
					ida = ctx.ack_sockets.Append(this);
									
				}
			}

			// called every time mtu_floor or mtu_ceiling are adjusted
			// this should be called every time we change mtu_floor or mtu_ceiling
			public void mtu_search_update()
            {
            	//assert(mtu_floor <= mtu_ceiling);
            
            	// binary search
            	mtu_last = (mtu_floor + mtu_ceiling) / 2;
            
            	// enable a new probe to be sent
            	mtu_probe_seq = mtu_probe_size = 0;
            
            	// if the floor and ceiling are close enough, consider the
            	// MTU binary search complete. We set the current value
            	// to floor since that's the only size we know can go through
            	// also set the ceiling to floor to terminate the searching
            	if (mtu_ceiling - mtu_floor <= 16) {
            		mtu_last = mtu_floor;
            	
            		mtu_ceiling = mtu_floor;
            		//assert(mtu_floor <= mtu_ceiling);
            		// Do another search in 30 minutes
            		mtu_discover_time = (long) UtpCallbacks.utp_call_get_milliseconds(this.ctx, this) + 30 * 60 * 1000;
            	}
            }
			
			
			public void mtu_reset()
            {
            	mtu_ceiling = get_udp_mtu();
            	// Less would not pass TCP...
            	mtu_floor = 576;
            	
            	//assert(mtu_floor <= mtu_ceiling);
            	mtu_discover_time = (long) UtpCallbacks.utp_call_get_milliseconds(this.ctx, this) + 30 * 60 * 1000;
            }

			// Calculates the current receive window
			public size_t get_rcv_window()
			{
				// Trim window down according to what's already in buffer.
				size_t numbuf = UtpCallbacks.utp_call_get_read_buffer_size(this.ctx, this);
				return opt_rcvbuf > numbuf ? opt_rcvbuf - numbuf : 0;
			}

			// Test if we're ready to decay max_window
			// XXX this breaks when spaced by > INT_MAX/2, which is 49
			// days; the failure mode in that case is we do an extra decay
			// or fail to do one when we really shouldn't.
			public bool can_decay_win(int64 msec) 
			{
		                return (msec - last_rwin_decay) >= MAX_WINDOW_DECAY;
			}

			// If we can, decay max window, returns true if we actually did so
			public void maybe_decay_win(uint64 current_ms)
			{
				if (can_decay_win(current_ms)) {
					// TCP uses 0.5
					max_window = (size_t)(max_window * .5);
					last_rwin_decay = current_ms;
					if (max_window < MIN_WINDOW_SIZE)
						max_window = MIN_WINDOW_SIZE;
					slow_start = false;
					ssthresh = max_window;
				}
			}

			public size_t get_header_size() 
			{
				return (size_t) sizeof(PacketFormatV1);
			}

			public size_t get_udp_mtu()
			{
				socklen_t len;
				SOCKADDR_STORAGE sa = addr.get_sockaddr_storage(len);
				return UtpCallbacks.utp_call_get_udp_mtu(this.ctx, this, sa, (int) len);
			}

			public size_t get_udp_overhead()
			{
				socklen_t len;
				SOCKADDR_STORAGE sa = addr.get_sockaddr_storage(&len);
				return UtpCallbacks.utp_call_get_udp_overhead(this.ctx, this, (const struct sockaddr *)&sa, len);
			}

			public size_t get_overhead()
			{
				return get_udp_overhead() + get_header_size();
			}

			public void send_data(byte* b, size_t length, bandwidth_type_t type, uint32 flags)
            {
            	// time stamp this packet with local time, the stamp goes into
            	// the header of every packet at the 8th byte for 8 bytes :
            	// two integers, check packet.h for more
            	uint64 time = (long) UtpCallbacks.utp_call_get_microseconds(ctx, this);
            
            	PacketFormatV1* b1 = (PacketFormatV1*)b;
            	b1.tv_usec = (uint32)time;
            	b1.reply_micro = reply_micro;
            
            	last_sent_packet = ctx.current_ms;
            
            	// #ifdef _DEBUG
            	// _stats.nbytes_xmit += length;
            	// ++_stats.nxmit;
            	// #endif
            
            	if (ctx.callbacks[UTP_ON_OVERHEAD_STATISTICS]) {
            		size_t n;
            		if (type == payload_bandwidth) {
            			// if this packet carries payload, just
            			// count the header as overhead
            			type = header_overhead;
            			n = get_overhead();
            		} else {
            			n = length + get_udp_overhead();
            		}
                    UtpCallbacks.utp_call_on_overhead_statistics(ctx, this, true, n, type);
            	}
				
            		send_to_addr(ctx, b, length, addr, flags);
            		removeSocketFromAckList(this);
            }
			

			public void send_ack(bool synack)
            {
            	PacketFormatAckV1 pfa = new PacketFormatAckV1();
            	//zeromem(&pfa);
            
            	size_t len;
            	last_rcv_win = get_rcv_window();
            	pfa.pf.set_version(1);
            	pfa.pf.set_type((byte) ST.ST_STATE);
            	pfa.pf.ext = 0;
            	pfa.pf.connid = conn_id_send;
            	pfa.pf.ack_nr = ack_nr;
            	pfa.pf.seq_nr = seq_nr;
            	pfa.pf.windowsize = (uint32)last_rcv_win;
            	len = sizeof(PacketFormatV1);
            
            	// we never need to send EACK for connections
            	// that are shutting down
            	if (reorder_count != 0 && !got_fin_reached) {
            		// if reorder count > 0, send an EACK.
            		// reorder count should always be 0
            		// for synacks, so this should not be
            		// as synack
            		//assert(!synack);
            		pfa.pf.ext = 1;
            		pfa.ext_next = 0;
            		pfa.ext_len = 4;
            		uint m = 0;
            
            		// reorder count should only be non-zero
            		// if the packet ack_nr + 1 has not yet
            		// been received
            		//assert(inbuf.get(ack_nr + 1) == null);
            		size_t window = min<size_t>(14+16, inbuf.size());
            		// Generate bit mask of segments received.
            		for (size_t i = 0; i < window; i++) {
            			if (inbuf.get(ack_nr + i + 2) != null) {
            				m |= 1 << i;
            
            				#if UTP_DEBUG_LOGGING
            				log(UTP_LOG_DEBUG, "EACK packet [%u]", ack_nr + i + 2);
            				#endif
            			}
            		}
            		pfa.acks[0] = (byte)m;
            		pfa.acks[1] = (byte)(m >> 8);
            		pfa.acks[2] = (byte)(m >> 16);
            		pfa.acks[3] = (byte)(m >> 24);
            		len += 4 + 2;
            
            		
            	} else {
            		
            	}
            
            	send_data((byte*)&pfa, len, ack_overhead);
            	removeSocketFromAckList(this);
            }
			
			
			

			public void send_keep_alive()
			{
				ack_nr--;

				

				send_ack(false);
				ack_nr++;
			}

			public static void send_rst(utp_context ctx,
			 PackedSockAddr addr, uint32 conn_id_send, uint16 ack_nr, uint16 seq_nr)
			{
				PacketFormatV1 pf1 = new PacketFormatV1();
				//zeromem(&pf1);

				size_t len;
				pf1.set_version(1);
				pf1.set_type(ST.ST_RESET);
				pf1.ext = 0;
				pf1.connid = conn_id_send;
				pf1.ack_nr = ack_nr;
				pf1.seq_nr = seq_nr;
				pf1.windowsize = 0;
				len = sizeof(PacketFormatV1);

				
				send_to_addr(ctx, (byte*)&pf1, len, addr);
			}

			public void send_packet(OutgoingPacket pkt)
			{
				// only count against the quota the first time we
				// send the packet. Don't enforce quota when closing
				// a socket. Only enforce the quota when we're sending
				// at slow rates (max window < packet size)

				//size_t max_send = min(max_window, opt_sndbuf, max_window_user);
				time_t cur_time = UtpCallbacks.utp_call_get_milliseconds(this.ctx, this);

				if (pkt.transmissions == 0 || pkt.need_resend) {
					cur_window += pkt.payload;
				}

				pkt.need_resend = false;

				PacketFormatV1* p1 = (PacketFormatV1*)pkt.data;
				p1.ack_nr = ack_nr;
				pkt.time_sent = (long) UtpCallbacks.utp_call_get_microseconds(this.ctx, this);

				//socklen_t salen;
				//SOCKADDR_STORAGE sa = addr.get_sockaddr_storage(&salen);
				bool use_as_mtu_probe = false;

				// TODO: this is subject to nasty wrapping issues! Below as well
 				if (mtu_discover_time < (uint64)cur_time) {
					// it's time to reset our MTU assupmtions
					// and trigger a new search
					mtu_reset();
				}

				// don't use packets that are larger then mtu_ceiling
				// as probes, since they were probably used as probes
				// already and failed, now we need it to fragment
				// just to get it through
				// if seq_nr == 1, the probe would end up being 0
				// which is a magic number representing no-probe
				// that why we don't send a probe for a packet with
				// sequence number 0
 				if (mtu_floor < mtu_ceiling
					&& pkt.length > mtu_floor
					&& pkt.length <= mtu_ceiling
					&& mtu_probe_seq == 0
					&& seq_nr != 1
					&& pkt.transmissions == 0) {

					// we've already incremented seq_nr
					// for this packet
 					mtu_probe_seq = (seq_nr - 1) & ACK_NR_MASK;
 					mtu_probe_size = pkt.length;
					//assert(pkt.length >= mtu_floor);
					//assert(pkt.length <= mtu_ceiling);
 					use_as_mtu_probe = true;
					
 				}

				pkt.transmissions++;
				send_data((byte*)pkt.data, pkt.length,
					(state == CONN_STATE.CS_SYN_SENT) ? connect_overhead
					: (pkt.transmissions == 1) ? payload_bandwidth
					: retransmit_overhead, use_as_mtu_probe ? UTP_UDP_DONTFRAG : 0);
			}

			
			public bool is_full(int bytes)
            {
            	size_t packet_size = get_packet_size();
            	if (bytes < 0) bytes = (int) packet_size;
            	else if (bytes > (int)packet_size) bytes = (int)packet_size;
            	size_t max_send = min(max_window, opt_sndbuf, max_window_user);
            
            	// subtract one to save space for the FIN packet
            	if (cur_window_packets >= OUTGOING_BUFFER_MAX_SIZE - 1) {
            
            		
            
            		last_maxed_out_window = (long) ctx.current_ms;
            		return true;
            	}
            
            	
            
            	if (cur_window + bytes > max_send) {
            		last_maxed_out_window = (long) ctx.current_ms;
            		return true;
            	}
            	return false;
            }
			
			
			
			public bool flush_packets()
			{
				size_t packet_size = get_packet_size();

				// send packets that are waiting on the pacer to be sent
				// i has to be an unsigned 16 bit counter to wrap correctly
				// signed types are not guaranteed to wrap the way you expect
				for (uint16 i = seq_nr - cur_window_packets; i != seq_nr; ++i) {
					OutgoingPacket *pkt = (OutgoingPacket*)outbuf.get(i);
					if (pkt == 0 || (pkt.transmissions > 0 && pkt.need_resend == false)) continue;
					// have we run out of quota?
					if (is_full()) return true;

					// Nagle check
					// don't send the last packet if we have one packet in-flight
					// and the current packet is still smaller than packet_size.
					if (i != ((seq_nr - 1) & ACK_NR_MASK) ||
					    cur_window_packets == 1 ||
					    pkt.payload >= packet_size) {
						send_packet(pkt);
					}
				}
				return false;
			}
			
			
			// @payload: number of bytes to send
			// @flags: either ST_DATA, or ST_FIN
			// @iovec: varBase address of iovec array
			// @num_iovecs: number of iovecs in array
			public void write_outgoing_packet(size_t payload, uint flags, utp_iovec iovec, size_t num_iovecs)
			{
				// Setup initial timeout timer
				if (cur_window_packets == 0) {
					retransmit_timeout = rto;
					rto_timeout = (long) ctx.current_ms + retransmit_timeout;
					//assert(cur_window == 0);
				}

				size_t packet_size = get_packet_size();
				do {
					//assert(cur_window_packets < OUTGOING_BUFFER_MAX_SIZE);
					//assert(flags == ST.ST_DATA || flags == ST.ST_FIN);

					size_t added = 0;

					OutgoingPacket pkt = null;

					if (cur_window_packets > 0) {
						pkt = (OutgoingPacket*)outbuf.get(seq_nr - 1);
					}

					const size_t header_size = get_header_size();
					bool append = true;

					// if there's any room left in the last packet in the window
					// and it hasn't been sent yet, fill that frame first
					if (payload!=null && pkt!=null && pkt.transmissions==0 && pkt.payload < packet_size) {
						// Use the previous unsent packet
						added = min(payload + pkt.payload, max<size_t>(packet_size, pkt.payload)) - pkt.payload;
						pkt = (OutgoingPacket*)realloc(pkt,
													   (sizeof(OutgoingPacket) - 1) +
													   header_size +
													   pkt.payload + added);
						outbuf.put(seq_nr - 1, pkt);
						append = false;
						//assert(!pkt.need_resend);
					} else {
						// Create the packet to send.
						added = payload;
						pkt = (OutgoingPacket*)malloc((sizeof(OutgoingPacket) - 1) +
													  header_size +
													  added);
						pkt.payload = 0;
						pkt.transmissions = 0;
						pkt.need_resend = false;
					}

					if (added > 0) {
						//assert(flags == ST.ST_DATA);

						// Fill it with data from the upper layer.
						char *p = pkt.data + header_size + pkt.payload;
						size_t needed = added;

						/*
						while (needed) {
							*p = *(char*)iovec[0].iov_varBase;
							p++;
							iovec[0].iov_varBase = (char *)iovec[0].iov_varBase + 1;
							needed--;
						}
						*/

						for (size_t i = 0; i < num_iovecs && needed; i++) {
							if (iovec[i].iov_len == 0)
								continue;

							size_t num = min<size_t>(needed, iovec[i].iov_len);
							memcpy(p, iovec[i].iov_varBase, num);

							p += num;

							iovec[i].iov_len -= num;
							iovec[i].iov_varBase = (byte*)iovec[i].iov_varBase + num;	// iovec[i].iov_varBase += num, but without void* pointers
							needed -= num;
						}

						//assert(needed == 0);
					}
					pkt.payload += added;
					pkt.length = header_size + pkt.payload;

					last_rcv_win = get_rcv_window();

					PacketFormatV1 p1 = (PacketFormatV1)pkt.data;
					p1.set_version(1);
					p1.set_type(flags);
					p1.ext = 0;
					p1.connid = conn_id_send;
					p1.windowsize = (uint32)last_rcv_win;
					p1.ack_nr = ack_nr;

					if (append) {
						// Remember the message in the outgoing queue.
						outbuf.ensure_size(seq_nr, cur_window_packets);
						outbuf.put(seq_nr, pkt);
						p1.seq_nr = seq_nr;
						seq_nr++;
						cur_window_packets++;
					}

					payload -= added;

				} while (payload > 0);

				flush_packets();
			}

			

			public void check_timeouts()
			{
				

				// this invariant should always be true
				//assert(cur_window_packets == 0 || outbuf.get(seq_nr - cur_window_packets));

				

				if (state != CONN_STATE.CS_DESTROY) flush_packets();

				switch (state) {
				case CONN_STATE.CS_SYN_SENT:
				case CONN_STATE.CS_SYN_RECV:
				case CONN_STATE.CS_CONNECTED_FULL:
				case CONN_STATE.CS_CONNECTED: {

					// Reset max window...
					if ((int)((long) ctx.current_ms - zerowindow_time) >= 0 && max_window_user == 0) {
						max_window_user = PACKET_SIZE;
					}

					if ((int)((long) ctx.current_ms - rto_timeout) >= 0
						&& rto_timeout > 0) {

						bool ignore_loss = false;

						if (cur_window_packets == 1
							&& ((seq_nr - 1) & ACK_NR_MASK) == mtu_probe_seq
							&& mtu_probe_seq != 0) {
							// we only had  a single outstanding packet that timed out, and it was the probe
							mtu_ceiling = mtu_probe_size - 1;
							mtu_search_update();
							// this packet was most likely dropped because the packet size being
							// too big and not because congestion. To accelerate the binary search for
							// the MTU, resend immediately and don't reset the window size
							ignore_loss = true;
							
						}
						// we dropepd the probe, clear these fields to
						// allow us to send a new one
						mtu_probe_seq = mtu_probe_size = 0;
						

						/*
						OutgoingPacket *pkt = (OutgoingPacket*)outbuf.get(seq_nr - cur_window_packets);

						// If there were a lot of retransmissions, force recomputation of round trip time
						if (pkt.transmissions >= 4)
							rtt = 0;
						*/

						// Increase RTO
						uint new_timeout = ignore_loss ? retransmit_timeout : retransmit_timeout * 2;

						// They initiated the connection but failed to respond before the rto. 
						// A malicious client can also spoof the destination address of a ST_SYN bringing us to this state.
						// Kill the connection and do not notify the upper layer
						if (state == CONN_STATE.CS_SYN_RECV) {
							state = CONN_STATE.CS_DESTROY;
							UtpCallbacks.utp_call_on_error(ctx, this, UTP_ETIMEDOUT);
							return;
						}

						// We initiated the connection but the other side failed to respond before the rto
						if (retransmit_count >= 4 || (state == CONN_STATE.CS_IDLE.CS_SYN_SENT && retransmit_count >= 2)) {
							// 4 consecutive transmissions have timed out. Kill it. If we
							// haven't even connected yet, give up after only 2 consecutive
							// failed transmissions.
							if (close_requested)
								state = CONN_STATE.CS_DESTROY;
							else
								state = CONN_STATE.CS_RESET;
							UtpCallbacks.utp_call_on_error(ctx, this, UTP_ETIMEDOUT);
							return;
						}

						retransmit_timeout = new_timeout;
						rto_timeout = (long) ctx.current_ms + new_timeout;

						if (!ignore_loss) {
							// On Timeout
							duplicate_ack = 0;

							int packet_size = (int) get_packet_size();

							if ((cur_window_packets == 0) && ((int)max_window > packet_size)) {
								// we don't have any packets in-flight, even though
								// we could. This implies that the connection is just
								// idling. No need to be aggressive about resetting the
								// congestion window. Just let it decay by a 3:rd.
								// don't set it any lower than the packet size though
								max_window = max(max_window * 2 / 3, size_t(packet_size));
							} else {
								// our delay was so high that our congestion window
								// was shrunk below one packet, preventing us from
								// sending anything for one time-out period. Now, reset
								// the congestion window to fit one packet, to start over
								// again
								max_window = packet_size;
								slow_start = true;
							}
						}

						// every packet should be considered lost
						for (int i = 0; i < cur_window_packets; ++i) {
							OutgoingPacket *pkt = (OutgoingPacket*)outbuf.get(seq_nr - i - 1);
							if (pkt == 0 || pkt.transmissions == 0 || pkt.need_resend) continue;
							pkt.need_resend = true;
							//assert(cur_window >= pkt.payload);
							cur_window -= pkt.payload;
						}

						if (cur_window_packets > 0) {
							retransmit_count++;
							

							fast_timeout = true;
							timeout_seq_nr = seq_nr;

							OutgoingPacket *pkt = (OutgoingPacket*)outbuf.get(seq_nr - cur_window_packets);
							//assert(pkt);

							// Re-send the packet.
							send_packet(pkt);
						}
					}

					// Mark the socket as writable. If the cwnd has grown, or if the number of
					// bytes in-flight is lower than cwnd, we need to make the socket writable again
					// in case it isn't
					if (state == CONN_STATE.CS_CONNECTED_FULL && !is_full()) {
						state = CONN_STATE.CS_CONNECTED;

						
						UtpCallbacks.utp_call_on_state_change(this.ctx, this, UTP_STATE_WRITABLE);
					}

					if (state >= CONN_STATE.CS_CONNECTED && !fin_sent) {
						if ((int)((long) ctx.current_ms - last_sent_packet) >= KEEPALIVE_INTERVAL) {
							send_keep_alive();
						}
					}
					break;
				}

				// prevent warning
				case CONN_STATE.CS_UNINITIALIZED:
				case CONN_STATE.CS_IDLE:
				case CONN_STATE.CS_RESET:
				case CONN_STATE.CS_DESTROY:
					break;
				}
			}			
			
			
			
			
			
			// returns:
			// 0: the packet was acked.
			// 1: it means that the packet had already been acked
			// 2: the packet has not been sent yet
			public int ack_packet(uint16 seq)
			{
				OutgoingPacket *pkt = (OutgoingPacket*)outbuf.get(seq);

				// the packet has already been acked (or not sent)
				if (pkt == null) {

					

					return 1;
				}

				// can't ack packets that haven't been sent yet!
				if (pkt.transmissions == 0) {

					

					return 2;
				}

				

				outbuf.put(seq, null);

				// if we never re-sent the packet, update the RTT estimate
				if (pkt.transmissions == 1) {
					// Estimate the round trip time.
					const uint32 ertt = (uint32)((UtpCallbacks.utp_call_get_microseconds(this.ctx, this) - pkt.time_sent) / 1000);
					if (rtt == 0) {
						// First round trip time sample
						rtt = ertt;
						rtt_var = ertt / 2;
						// sanity check. rtt should never be more than 6 seconds
			//			//assert(rtt < 6000);
					} else {
						// Compute new round trip times
						int delta = (int)rtt - (int) ertt;
						rtt_var = rtt_var + (int)(abs(delta) - rtt_var) / 4;
						rtt = (uint) (rtt - rtt/8 + ertt/8);
						// sanity check. rtt should never be more than 6 seconds
			//			//assert(rtt < 6000);
						rtt_hist.add_sample(ertt, ctx.current_ms);
					}
					rto = max<uint>(rtt + rtt_var * 4, 1000);

					

				}
				retransmit_timeout = rto;
				rto_timeout = (long) ctx.current_ms + rto;
				// if need_resend is set, this packet has already
				// been considered timed-out, and is not included in
				// the cur_window anymore
				if (!pkt.need_resend) {
					//assert(cur_window >= pkt.payload);
					cur_window -= pkt.payload;
				}
				retransmit_count = 0;
				return 0;
			}
			
			
			// count the number of bytes that were acked by the EACK header
			public size_t selective_ack_bytes(uint varBase, byte* mask, byte len, int64& min_rtt)
			{
				if (cur_window_packets == 0) return 0;

				size_t acked_bytes = 0;
				int bits = len * 8;
				uint64 now = UtpCallbacks.utp_call_get_microseconds(this.ctx, this);

				do {
					long v = varBase + bits;

					// ignore bits that haven't been sent yet
					// see comment in UTPSocket::selective_ack
					if (((seq_nr - v - 1) & ACK_NR_MASK) >= (uint16)(cur_window_packets - 1))
						continue;

					// ignore bits that represents packets we haven't sent yet
					// or packets that have already been acked
					OutgoingPacket pkt = (OutgoingPacket)outbuf.get(v);
					if (pkt == null || pkt.transmissions == 0)
						continue;

					// Count the number of segments that were successfully received past it.
					if (bits >= 0 && mask[bits>>3] & (1 << (bits & 7))) {
						//assert((int)(pkt.payload) >= 0);
						acked_bytes += pkt.payload;
						if (pkt.time_sent < now)
							min_rtt = min<int64>(min_rtt, now - pkt.time_sent);
						else
							min_rtt = min<int64>(min_rtt, 50000);
						continue;
					}
				} while (--bits >= -1);
				return acked_bytes;
			}
				
				
				
			
			public void selective_ack(uint varBase,  byte *mask, byte len)
			{
				if (cur_window_packets == 0) return;

				// the range is inclusive [0, 31] bits
				int bits = len * 8 - 1;

				int count = 0;

				// resends is a stack of sequence numbers we need to resend. Since we
				// iterate in reverse over the acked packets, at the end, the top packets
				// are the ones we want to resend
				int resends[MAX_EACK];
				int nr = 0;

				

				do {
					// we're iterating over the bits from higher sequence numbers
					// to lower (kind of in reverse order, wich might not be very
					// intuitive)
					uint v = varvarBase + bits;

					// ignore bits that haven't been sent yet
					// and bits that fall below the ACKed sequence number
					// this can happen if an EACK message gets
					// reordered and arrives after a packet that ACKs up past
					// the varBase for thie EACK message

					// this is essentially the same as:
					// if v >= seq_nr || v <= seq_nr - cur_window_packets
					// but it takes wrapping into account

					// if v == seq_nr the -1 will make it wrap. if v > seq_nr
					// it will also wrap (since it will fall further below 0)
					// and be > cur_window_packets.
					// if v == seq_nr - cur_window_packets, the result will be
					// seq_nr - (seq_nr - cur_window_packets) - 1
					// == seq_nr - seq_nr + cur_window_packets - 1
					// == cur_window_packets - 1 which will be caught by the
					// test. If v < seq_nr - cur_window_packets the result will grow
					// fall furhter outside of the cur_window_packets range.

					// sequence number space:
					//
					//     rejected <   accepted   > rejected
					// <============+--------------+============>
					//              ^              ^
					//              |              |
					//        (seq_nr-wnd)         seq_nr

					if (((seq_nr - v - 1) & ACK_NR_MASK) >= (uint16)(cur_window_packets - 1))
						continue;

					// this counts as a duplicate ack, even though we might have
					// received an ack for this packet previously (in another EACK
					// message for instance)
					bool bit_set = bits >= 0 && mask[bits>>3] & (1 << (bits & 7));

					// if this packet is acked, it counts towards the duplicate ack counter
					if (bit_set) count++;

					// ignore bits that represents packets we haven't sent yet
					// or packets that have already been acked
					OutgoingPacket pkt = (OutgoingPacket)outbuf.get(v);
					if (pkt == null || pkt.transmissions == 0) {

						
						continue;
					}

					// Count the number of segments that were successfully received past it.
					if (bit_set) {
						// the selective ack should never ACK the packet we're waiting for to decrement cur_window_packets
						//assert((v & outbuf.mask) != ((seq_nr - cur_window_packets) & outbuf.mask));
						ack_packet(v);
						continue;
					}

					// Resend segments
					// if count is less than our re-send limit, we haven't seen enough
					// acked packets in front of this one to warrant a re-send.
					// if count == 0, we're still going through the tail of zeroes
					if (((v - fast_resend_seq_nr) & ACK_NR_MASK) <= OUTGOING_BUFFER_MAX_SIZE &&
						count >= DUPLICATE_ACKS_BEFORE_RESEND) {
						// resends is a stack, and we're mostly interested in the top of it
						// if we're full, just throw away the lower half
						if (nr >= MAX_EACK - 2) {
							memmove(resends, &resends[MAX_EACK/2], MAX_EACK/2 * sizeof(resends[0]));
							nr -= MAX_EACK / 2;
						}
						resends[nr++] = v;

						

					} 
				} while (--bits >= -1);

				if (((varBase - 1 - fast_resend_seq_nr) & ACK_NR_MASK) <= OUTGOING_BUFFER_MAX_SIZE &&
					count >= DUPLICATE_ACKS_BEFORE_RESEND) {
					// if we get enough duplicate acks to start
					// resending, the first packet we should resend
					// is varBase-1
					resends[nr++] = (varBase - 1) & ACK_NR_MASK;

					

				} 

				bool back_off = false;
				int i = 0;
				while (nr > 0) {
					uint v = resends[--nr];
					// don't consider the tail of 0:es to be lost packets
					// only unacked packets with acked packets after should
					// be considered lost
					OutgoingPacket *pkt = (OutgoingPacket*)outbuf.get(v);

					// this may be an old (re-ordered) packet, and some of the
					// packets in here may have been acked already. In which
					// case they will not be in the send queue anymore
					if (!pkt) continue;

					

					// On Loss
					back_off = true;

					

					send_packet(pkt);
					fast_resend_seq_nr = (v + 1) & ACK_NR_MASK;

					// Re-send max 4 packets.
					if (++i >= 4) break;
				}

				if (back_off)
					maybe_decay_win(ctx.current_ms);

				duplicate_ack = count;
			}
			
			
			
			public void apply_ccontrol(size_t bytes_acked, uint32 actual_delay, int64 min_rtt)
			{
				// the delay can never be greater than the rtt. The min_rtt
				// variable is the RTT in microseconds

				//assert(min_rtt >= 0);
				int32 our_delay = min<uint32>(our_hist.get_value(), uint32(min_rtt));
				//assert(our_delay != INT_MAX);
				//assert(our_delay >= 0);

				UtpCallbacks.utp_call_on_delay_sample(this.ctx, this, our_delay / 1000);

				// This test the connection under heavy load from foreground
				// traffic. Pretend that our delays are very high to force the
				// connection to use sub-packet size window sizes
				//our_delay *= 4;

				// target is microseconds
				int target = target_delay;
				if (target <= 0) target = 100000;

				// this is here to compensate for very large clock drift that affects
				// the congestion controller into giving certain endpoints an unfair
				// share of the bandwidth. We have an estimate of the clock drift
				// (clock_drift). The unit of this is microseconds per 5 seconds.
				// empirically, a reasonable cut-off appears to be about 200000
				// (which is pretty high). The main purpose is to compensate for
				// people trying to "cheat" uTP by making their clock run slower,
				// and this definitely catches that without any risk of false positives
				// if clock_drift < -200000 start applying a penalty delay proportional
				// to how far beoynd -200000 the clock drift is
				int32 penalty = 0;
				if (clock_drift < -200000) {
					penalty = (-clock_drift - 200000) / 7;
					our_delay += penalty;
				}

				double off_target = target - our_delay;

				// this is the same as:
				//
				//    (min(off_target, target) / target) * (bytes_acked / max_window) * MAX_CWND_INCREASE_BYTES_PER_RTT
				//
				// so, it's scaling the max increase by the fraction of the window this ack represents, and the fraction
				// of the target delay the current delay represents.
				// The min() around off_target protects against crazy values of our_delay, which may happen when th
				// timestamps wraps, or by just having a malicious peer sending garbage. This caps the increase
				// of the window size to MAX_CWND_INCREASE_BYTES_PER_RTT per rtt.
				// as for large negative numbers, this direction is already capped at the min packet size further down
				// the min around the bytes_acked protects against the case where the window size was recently
				// shrunk and the number of acked bytes exceeds that. This is considered no more than one full
				// window, in order to keep the gain within sane boundries.

				//assert(bytes_acked > 0);
				double window_factor = (double)min(bytes_acked, max_window) / (double)max(max_window, bytes_acked);

				double delay_factor = off_target / target;
				double scaled_gain = MAX_CWND_INCREASE_BYTES_PER_RTT * window_factor * delay_factor;

				// since MAX_CWND_INCREASE_BYTES_PER_RTT is a cap on how much the window size (max_window)
				// may increase per RTT, we may not increase the window size more than that proportional
				// to the number of bytes that were acked, so that once one window has been acked (one rtt)
				// the increase limit is not exceeded
				// the +1. is to allow for floating point imprecision
				//assert(scaled_gain <= 1. + MAX_CWND_INCREASE_BYTES_PER_RTT * (double)min(bytes_acked, max_window) / (double)max(max_window, bytes_acked));

				if (scaled_gain > 0 && ((long)ctx.current_ms - last_maxed_out_window > 1000) ) {
					// if it was more than 1 second since we tried to send a packet
					// and stopped because we hit the max window, we're most likely rate
					// limited (which prevents us from ever hitting the window size)
					// if this is the case, we cannot let the max_window grow indefinitely
					scaled_gain = 0;
				}

				size_t ledbat_cwnd = (max_window + scaled_gain < MIN_WINDOW_SIZE) ? MIN_WINDOW_SIZE : (size_t)(max_window + scaled_gain);

				if (slow_start) {
					size_t ss_cwnd = (size_t)(max_window + window_factor*get_packet_size());
					if (ss_cwnd > ssthresh) {
						slow_start = false;
					} else if (our_delay > target*0.9) {
						// even if we're a little under the target delay, we conservatively
						// discontinue the slow start phase
						slow_start = false;
						ssthresh = max_window;
					} else {
						max_window = max(ss_cwnd, ledbat_cwnd);
					}
				} else {
					max_window = ledbat_cwnd;
				}


				// make sure that the congestion window is below max
				// make sure that we don't shrink our window too small
				max_window = clamp<size_t>(max_window, MIN_WINDOW_SIZE, opt_sndbuf);
			}
			
			
			
			
			// returns the max number of bytes of payload the uTP
            // connection is allowed to send
            public size_t get_packet_size() 
            {
            	int header_size = sizeof(PacketFormatV1);
            	size_t mtu = mtu_last ? mtu_last : mtu_ceiling;
            	return mtu - header_size;
            }
		};

		public void removeSocketFromAckList(UTPSocket conn)
		{
			if (conn.ida >= 0)
			{
				UTPSocket last = conn.ctx.ack_sockets[conn.ctx.ack_sockets.GetCount() - 1];

				// //assert(last.ida < (int)(conn.ctx.ack_sockets.GetCount()));
				// //assert(conn.ctx.ack_sockets[last.ida] == last);
				last.ida = conn.ida;
				conn.ctx.ack_sockets[conn.ida] = last;
				conn.ida = -1;

				// Decrease the count
				conn.ctx.ack_sockets.SetCount(conn.ctx.ack_sockets.GetCount() - 1);
			}
		}

		public static void utp_register_sent_packet(utp_context ctx, size_t length)
		{
			if (length <= PACKET_SIZE_MID) {
				if (length <= PACKET_SIZE_EMPTY) {
					ctx.context_stats._nraw_send[PACKET_SIZE_EMPTY_BUCKET]++;
				} else if (length <= PACKET_SIZE_SMALL) {
					ctx.context_stats._nraw_send[PACKET_SIZE_SMALL_BUCKET]++;
				} else
					ctx.context_stats._nraw_send[PACKET_SIZE_MID_BUCKET]++;
			} else {
				if (length <= PACKET_SIZE_BIG) {
					ctx.context_stats._nraw_send[PACKET_SIZE_BIG_BUCKET]++;
				} else
					ctx.context_stats._nraw_send[PACKET_SIZE_HUGE_BUCKET]++;
			}
		}

		public void send_to_addr(utp_context ctx, const byte *p, size_t len, const PackedSockAddr &addr, int flags)
		{
			socklen_t tolen;
			SOCKADDR_STORAGE to = addr.get_sockaddr_storage(&tolen);
			utp_register_sent_packet(ctx, len);
			UtpCallbacks.utp_call_sendto(ctx, null, p, len, (const struct sockaddr *)&to, tolen, flags);
		}









		public static void utp_register_recv_packet(UTPSocket conn, size_t len)
		{
			// #ifdef _DEBUG
			// ++conn._stats.nrecv;
			// conn._stats.nbytes_recv += len;
			// #endif

			if (len <= PACKET_SIZE_MID) {
				if (len <= PACKET_SIZE_EMPTY) {
					conn.ctx.context_stats._nraw_recv[PACKET_SIZE_EMPTY_BUCKET]++;
				} else if (len <= PACKET_SIZE_SMALL) {
					conn.ctx.context_stats._nraw_recv[PACKET_SIZE_SMALL_BUCKET]++;
				} else
					conn.ctx.context_stats._nraw_recv[PACKET_SIZE_MID_BUCKET]++;
			} else {
				if (len <= PACKET_SIZE_BIG) {
					conn.ctx.context_stats._nraw_recv[PACKET_SIZE_BIG_BUCKET]++;
				} else
					conn.ctx.context_stats._nraw_recv[PACKET_SIZE_HUGE_BUCKET]++;
			}
		}



		// Process an incoming packet
		// syn is true if this is the first packet received. It will cut off parsing
		// as soon as the header is done
		public size_t utp_process_incoming(UTPSocket conn, byte *packet, size_t len, bool syn = false)
		{
			utp_register_recv_packet(conn, len);

			conn.ctx.current_ms = UtpCallbacks.utp_call_get_milliseconds(conn.ctx, conn);

			PacketFormatV1 *pf1 = (PacketFormatV1*)packet;
			byte *packet_end = packet + len;

			uint16 pk_seq_nr = pf1.seq_nr;
			uint16 pk_ack_nr = pf1.ack_nr;
			uint8 pk_flags   = pf1.type();

			if (pk_flags >= ST.ST_NUM_STATES) return 0;

			// mark receipt time
			uint64 time = UtpCallbacks.utp_call_get_microseconds(conn.ctx, conn);

			// window packets size is used to calculate a minimum
			// permissible range for received acks. connections with acks falling
			// out of this range are dropped
			const uint16 curr_window = max<uint16>(conn.cur_window_packets + ACK_NR_ALLOWED_WINDOW, ACK_NR_ALLOWED_WINDOW);

			// ignore packets whose ack_nr is invalid. This would imply a spoofed address
			// or a malicious attempt to attach the uTP implementation.
			// acking a packet that hasn't been sent yet!
			// SYN packets have an exception, since there are no previous packets
			if ((pk_flags != ST.ST_SYN || conn.state != CONN_STATE.CS_SYN_RECV) && 
				(wrapping_compare_less(conn.seq_nr - 1, pk_ack_nr, ACK_NR_MASK)
				|| wrapping_compare_less(pk_ack_nr, conn.seq_nr - 1 - curr_window, ACK_NR_MASK))) {
				return 0;
			}

			// RSTs are handled earlier, since the connid matches the send id not the recv id
			//assert(pk_flags != ST.ST_RESET);

			// TODO: maybe send a ST_RESET if we're in CS_RESET?

			const byte *selack_ptr = null;

			// Unpack UTP packet options
			// Data pointer
			const byte *data = (const byte*)pf1 + conn.get_header_size();
			if (conn.get_header_size() > len) {

				return 0;
			}
			// Skip the extension headers
			uint extension = pf1.ext;
			if (extension != 0) {
				do {
					// Verify that the packet is valid.
					data += 2;

					if ((int)(packet_end - data) < 0 || (int)(packet_end - data) < data[-1]) {

						

						return 0;
					}

					switch(extension) {
					case 1: // Selective Acknowledgment
						selack_ptr = data;
						break;
					case 2: // extension bits
						if (data[-1] != 8) {

							#if UTP_DEBUG_LOGGING
							conn.log(UTP_LOG_DEBUG, "Invalid len of extension bits header");
							#endif

							return 0;
						}
						memcpy(conn.extensions, data, 8);
					}
					extension = data[-2];
					data += data[-1];
				} while (extension);
			}

			if (conn.state == CONN_STATE.CS_SYN_SENT) {
				// if this is a syn-ack, initialize our ack_nr
				// to match the sequence number we got from
				// the other end
				conn.ack_nr = (pk_seq_nr - 1) & SEQ_NR_MASK;
			}

			conn.last_got_packet = (long) conn.ctx.current_ms;

			if (syn) {
				return 0;
			}

			// seqnr is the number of packets past the expected
			// packet this is. ack_nr is the last acked, seq_nr is the
			// current. Subtracring 1 makes 0 mean "this is the next
			// expected packet".
			long seqnr = (pk_seq_nr - conn.ack_nr - 1) & SEQ_NR_MASK;

			// Getting an invalid sequence number?
			if (seqnr >= REORDER_BUFFER_MAX_SIZE) {
				if (seqnr >= (SEQ_NR_MASK + 1) - REORDER_BUFFER_MAX_SIZE && pk_flags != ST.ST_STATE) {
					conn.schedule_ack();
				}

				
				return 0;
			}

			// Process acknowledgment
			// acks is the number of packets that was acked
			long acks = (pk_ack_nr - (conn.seq_nr - 1 - conn.cur_window_packets)) & ACK_NR_MASK;

			// this happens when we receive an old ack nr
			if (acks > conn.cur_window_packets) acks = 0;

			// if we get the same ack_nr as in the last packet
			// increase the duplicate_ack counter, otherwise reset
			// it to 0.
			// It's important to only count ACKs in ST_STATE packets. Any other
			// packet (primarily ST_DATA) is likely to have been sent because of the
			// other end having new outgoing data, not in response to incoming data.
			// For instance, if we're receiving a steady stream of payload with no
			// outgoing data, and we suddently have a few bytes of payload to send (say,
			// a bittorrent HAVE message), we're very likely to see 3 duplicate ACKs
			// immediately after sending our payload packet. This effectively disables
			// the fast-resend on duplicate-ack logic for bi-directional connections
			// (except in the case of a selective ACK). This is in line with BSD4.4 TCP
			// implementation.
			if (conn.cur_window_packets > 0) {
				if (pk_ack_nr == ((conn.seq_nr - conn.cur_window_packets - 1) & ACK_NR_MASK)
					&& conn.cur_window_packets > 0
					&& pk_flags == ST.ST_STATE) {
					++conn.duplicate_ack;
					if (conn.duplicate_ack == DUPLICATE_ACKS_BEFORE_RESEND) {
						// It's likely that the probe was rejected due to its size, but we haven't got an
						// ICMP report back yet
						if (pk_ack_nr == ((conn.mtu_probe_seq - 1) & ACK_NR_MASK)) {
							conn.mtu_ceiling = conn.mtu_probe_size - 1;
							conn.mtu_search_update();
						} else {
							// A non-probe was blocked before our probe.
							// Can't conclude much, send a new probe
							conn.mtu_probe_seq = conn.mtu_probe_size = 0;
						}
					}
				} else {
					conn.duplicate_ack = 0;
				}

				// TODO: if duplicate_ack == DUPLICATE_ACK_BEFORE_RESEND
				// and fast_resend_seq_nr <= ack_nr + 1
				//    resend ack_nr + 1
				// also call maybe_decay_win()
			}

			// figure out how many bytes were acked
			size_t acked_bytes = 0;

			// the minimum rtt of all acks
			// this is the upper limit on the delay we get back
			// from the other peer. Our delay cannot exceed
			// the rtt of the packet. If it does, clamp it.
			// this is done in apply_ledbat_ccontrol()
			int64 min_rtt = INT64_MAX;

			uint64 now = UtpCallbacks.utp_call_get_microseconds(conn.ctx, conn);

			for (int i = 0; i < acks; ++i) {
				uint seq = (conn.seq_nr - conn.cur_window_packets + i) & ACK_NR_MASK;
				OutgoingPacket pkt = (OutgoingPacket)conn.outbuf.get(seq);
				if (pkt == 0 || pkt.transmissions == 0) continue;
				//assert((int)(pkt.payload) >= 0);
				acked_bytes += pkt.payload;
				if (conn.mtu_probe_seq && seq == conn.mtu_probe_seq) {
					conn.mtu_floor = conn.mtu_probe_size;
					conn.mtu_search_update();
				}

				// in case our clock is not monotonic
				if (pkt.time_sent < now)
					min_rtt = min<int64>(min_rtt, now - pkt.time_sent);
				else
					min_rtt = min<int64>(min_rtt, 50000);
			}

			// count bytes acked by EACK
			if (selack_ptr != null) {
				acked_bytes += conn.selective_ack_bytes((pk_ack_nr + 2) & ACK_NR_MASK,
														 selack_ptr, selack_ptr[-1], min_rtt);
			}

			

			uint64 p = pf1.tv_usec;

			conn.last_measured_delay = conn.ctx.current_ms;

			// get delay in both directions
			// record the delay to report back
			uint32 their_delay = (uint32)(p == 0 ? 0 : time - p);
			conn.reply_micro = their_delay;
			uint32 prev_delay_varBase = conn.their_hist.delay_varBase;
			if (their_delay != 0) conn.their_hist.add_sample(their_delay, conn.ctx.current_ms);

			// if their new delay varBase is less than their previous one
			// we should shift our delay varBase in the other direction in order
			// to take the clock skew into account
			if (prev_delay_varBase != 0 &&
				wrapping_compare_less(conn.their_hist.delay_varBase, prev_delay_varBase, TIMESTAMP_MASK)) {
				// never adjust more than 10 milliseconds
				if (prev_delay_varBase - conn.their_hist.delay_varBase <= 10000) {
					conn.our_hist.shift(prev_delay_varBase - conn.their_hist.delay_varBase);
				}
			}

			uint32 actual_delay = (uint32(pf1.reply_micro)==INT_MAX?0:uint32(pf1.reply_micro));

			// if the actual delay is 0, it means the other end
			// hasn't received a sample from us yet, and doesn't
			// know what it is. We can't update out history unless
			// we have a true measured sample
			if (actual_delay != 0) {
				conn.our_hist.add_sample(actual_delay, conn.ctx.current_ms);

				// this is keeping an average of the delay samples
				// we've recevied within the last 5 seconds. We sum
				// all the samples and increase the count in order to
				// calculate the average every 5 seconds. The samples
				// are varBased off of the average_delay_varBase to deal with
				// wrapping counters.
				if (conn.average_delay_varBase == 0) conn.average_delay_varBase = actual_delay;
				int64 average_delay_sample = 0;
				// distance walking from lhs to rhs, downwards
				uint32 dist_down = conn.average_delay_varBase - actual_delay;
				// distance walking from lhs to rhs, upwards
				uint32 dist_up = actual_delay - conn.average_delay_varBase;

				if (dist_down > dist_up) {
		//			//assert(dist_up < INT_MAX / 4);
					// average_delay_varBase < actual_delay, we should end up
					// with a positive sample
					average_delay_sample = dist_up;
				} else {
		//			//assert(-int64(dist_down) < INT_MAX / 4);
					// average_delay_varBase >= actual_delay, we should end up
					// with a negative sample
					average_delay_sample = -int64(dist_down);
				}
				conn.current_delay_sum += average_delay_sample;
				++conn.current_delay_samples;

				if ((long) conn.ctx.current_ms > conn.average_sample_time) {

					int32 prev_average_delay = conn.average_delay;

					//assert(conn.current_delay_sum / conn.current_delay_samples < INT_MAX);
					//assert(conn.current_delay_sum / conn.current_delay_samples > -INT_MAX);
					// write the new average
					conn.average_delay = (int32)(conn.current_delay_sum / conn.current_delay_samples);
					// each slot represents 5 seconds
					conn.average_sample_time += 5000;

					conn.current_delay_sum = 0;
					conn.current_delay_samples = 0;

					// this makes things very confusing when logging the average delay
		//#if !g_log_utp
					// normalize the average samples
					// since we're only interested in the slope
					// of the curve formed by the average delay samples,
					// we can cancel out the actual offset to make sure
					// we won't have problems with wrapping.
					int min_sample = min(prev_average_delay, conn.average_delay);
					int max_sample = max(prev_average_delay, conn.average_delay);

					// normalize around zero. Try to keep the min <= 0 and max >= 0
					int adjust = 0;
					if (min_sample > 0) {
						// adjust all samples (and the varBaseline) down by min_sample
						adjust = -min_sample;
					} else if (max_sample < 0) {
						// adjust all samples (and the varBaseline) up by -max_sample
						adjust = -max_sample;
					}
					if (adjust > 0) {
						conn.average_delay_varBase -= adjust;
						conn.average_delay += adjust;
						prev_average_delay += adjust;
					}
		//#endif

					// update the clock drift estimate
					// the unit is microseconds per 5 seconds
					// what we're doing is just calculating the average of the
					// difference between each slot. Since each slot is 5 seconds
					// and the timestamps unit are microseconds, we'll end up with
					// the average slope across our history. If there is a consistent
					// trend, it will show up in this value

					//int64 slope = 0;
					int32 drift = conn.average_delay - prev_average_delay;

					// clock_drift is a rolling average
					conn.clock_drift = ((conn.clock_drift) * 7 + drift) / 8;
					conn.clock_drift_raw = drift;
				}
			}

			// if our new delay varBase is less than our previous one
			// we should shift the other end's delay varBase in the other
			// direction in order to take the clock skew into account
			// This is commented out because it creates bad interactions
			// with our adjustment in the other direction. We don't really
			// need our estimates of the other peer to be very accurate
			// anyway. The problem with shifting here is that we're more
			// likely shift it back later because of a low latency. This
			// second shift back would cause us to shift our delay varBase
			// which then get's into a death spiral of shifting delay varBases
		/*	if (prev_delay_varBase != 0 &&
				wrapping_compare_less(conn.our_hist.delay_varBase, prev_delay_varBase)) {
				// never adjust more than 10 milliseconds
				if (prev_delay_varBase - conn.our_hist.delay_varBase <= 10000) {
					conn.their_hist.Shift(prev_delay_varBase - conn.our_hist.delay_varBase);
				}
			}
		*/

			// if the delay estimate exceeds the RTT, adjust the varBase_delay to
			// compensate
			//assert(min_rtt >= 0);
			if ((conn.our_hist.get_value()) > min_rtt) {
				conn.our_hist.shift((uint32)(conn.our_hist.get_value() - min_rtt));
			}

			// only apply the congestion controller on acks
			// if we don't have a delay measurement, there's
			// no point in invoking the congestion control
			if (actual_delay != 0 && acked_bytes >= 1)
				conn.apply_ccontrol(acked_bytes, actual_delay, min_rtt);

			// sanity check, the other end should never ack packets
			// past the point we've sent
			if (acks <= conn.cur_window_packets) {
				conn.max_window_user = pf1.windowsize;

				// If max user window is set to 0, then we startup a timer
				// That will reset it to 1 after 15 seconds.
				if (conn.max_window_user == 0)
					// Reset max_window_user to 1 every 15 seconds.
					conn.zerowindow_time = (long) conn.ctx.current_ms + 15000;

				// Respond to connect message
				// Switch to CONNECTED state.
				// If this is an ack and we're in still handshaking
				// transition over to the connected state.

				// Incoming connection completion
				if (pk_flags == ST.ST_DATA && conn.state == CONN_STATE.CS_SYN_RECV) {
					conn.state = CONN_STATE.CS_CONNECTED;
				}

				// Outgoing connection completion
				if (pk_flags == ST.ST_STATE && conn.state == CONN_STATE.CS_SYN_SENT)	{
					conn.state = CONN_STATE.CS_CONNECTED;
				
					// If the user has defined the ON_CONNECT callback, use that to
					// notify the user that the socket is now connected.  If ON_CONNECT
					// has not been defined, notify the user via ON_STATE_CHANGE.
					if (conn.ctx.callbacks[UTP_ON_CONNECT])
						UtpCallbacks.utp_call_on_connect(conn.ctx, conn);
					else
						UtpCallbacks.utp_call_on_state_change(conn.ctx, conn, UTP_STATE_CONNECT);

				// We've sent a fin, and everything was ACKed (including the FIN).
				// cur_window_packets == acks means that this packet acked all 
				// the remaining packets that were in-flight.
				} else if (conn.fin_sent && conn.cur_window_packets == acks) {
					conn.fin_sent_acked = true;
					if (conn.close_requested) {
						conn.state = CONN_STATE.CS_DESTROY;
					}
				}

				// Update fast resend counter
				if (wrapping_compare_less(conn.fast_resend_seq_nr
					, (pk_ack_nr + 1) & ACK_NR_MASK, ACK_NR_MASK))
					conn.fast_resend_seq_nr = (pk_ack_nr + 1) & ACK_NR_MASK;

				#if UTP_DEBUG_LOGGING
				conn.log(UTP_LOG_DEBUG, "fast_resend_seq_nr:%u", conn.fast_resend_seq_nr);
				#endif

				for (int i = 0; i < acks; ++i) {
					int ack_status = conn.ack_packet(conn.seq_nr - conn.cur_window_packets);
					// if ack_status is 0, the packet was acked.
					// if acl_stauts is 1, it means that the packet had already been acked
					// if it's 2, the packet has not been sent yet
					// We need to break this loop in the latter case. This could potentially
					// happen if we get an ack_nr that does not exceed what we have stuffed
					// into the outgoing buffer, but does exceed what we have sent
					if (ack_status == 2) {
						break;
					}
					conn.cur_window_packets--;

				}

				// #ifdef _DEBUG
				// if (conn.cur_window_packets == 0)
				// 	//assert(conn.cur_window == 0);
				// #endif

				// packets in front of this may have been acked by a
				// selective ack (EACK). Keep decreasing the window packet size
				// until we hit a packet that is still waiting to be acked
				// in the send queue
				// this is especially likely to happen when the other end
				// has the EACK send bug older versions of uTP had
				while (conn.cur_window_packets > 0 && !conn.outbuf.get(conn.seq_nr - conn.cur_window_packets)) {
					conn.cur_window_packets--;

					

				}

				// this invariant should always be true
				//assert(conn.cur_window_packets == 0 || conn.outbuf.get(conn.seq_nr - conn.cur_window_packets));

				// flush Nagle
				if (conn.cur_window_packets == 1) {
					OutgoingPacket pkt = (OutgoingPacket)conn.outbuf.get(conn.seq_nr - 1);
					// do we still have quota?
					if (pkt.transmissions == 0) {
						conn.send_packet(pkt);
					}
				}

				// Fast timeout-retry
				if (conn.fast_timeout) {

					#if UTP_DEBUG_LOGGING
					conn.log(UTP_LOG_DEBUG, "Fast timeout %u,%u,%u?", (uint)conn.cur_window, conn.seq_nr - conn.timeout_seq_nr, conn.timeout_seq_nr);
					#endif

					// if the fast_resend_seq_nr is not pointing to the oldest outstanding packet, it suggests that we've already
					// resent the packet that timed out, and we should leave the fast-timeout mode.
					if (((conn.seq_nr - conn.cur_window_packets) & ACK_NR_MASK) != conn.fast_resend_seq_nr) {
						conn.fast_timeout = false;
					} else {
						// resend the oldest packet and increment fast_resend_seq_nr
						// to not allow another fast resend on it again
						OutgoingPacket pkt = (OutgoingPacket)conn.outbuf.get(conn.seq_nr - conn.cur_window_packets);
						if (pkt!=null && pkt.transmissions > 0) {

							conn.fast_resend_seq_nr++;
							conn.send_packet(pkt);
						}
					}
				}
			}

			// Process selective acknowledgent
			if (selack_ptr != null) {
				conn.selective_ack((uint) pk_ack_nr + 2, selack_ptr, selack_ptr[-1]);
			}

			// this invariant should always be true
			//assert(conn.cur_window_packets == 0 || conn.outbuf.get(conn.seq_nr - conn.cur_window_packets));

			

			// In case the ack dropped the current window below
			// the max_window size, Mark the socket as writable
			if (conn.state == CONN_STATE.CS_CONNECTED_FULL && !conn.is_full()) {
				conn.state = CONN_STATE.CS_CONNECTED;
				#if UTP_DEBUG_LOGGING
				conn.log(UTP_LOG_DEBUG, "Socket writable. max_window:%u cur_window:%u packet_size:%u",
					(uint)conn.max_window, (uint)conn.cur_window, (uint)conn.get_packet_size());
				#endif
				UtpCallbacks.utp_call_on_state_change(conn.ctx, conn, UTP_STATE_WRITABLE);
			}

			if (pk_flags == ST.ST_STATE) {
				// This is a state packet only.
				return 0;
			}

			// The connection is not in a state that can accept data?
			if (conn.state != CONN_STATE.CS_CONNECTED &&
				conn.state != CONN_STATE.CS_CONNECTED_FULL) {
				return 0;
			}

			// Is this a finalize packet?
			if (pk_flags == ST.ST_FIN && !conn.got_fin) {

				

				conn.got_fin = true;
				conn.eof_pkt = pk_seq_nr;
				// at this point, it is possible for the
				// other end to have sent packets with
				// sequence numbers higher than seq_nr.
				// if this is the case, our reorder_count
				// is out of sync. This case is dealt with
				// when we re-order and hit the eof_pkt.
				// we'll just ignore any packets with
				// sequence numbers past this
			}

			// Getting an in-order packet?
			if (seqnr == 0) {
				size_t count = packet_end - data;
				if (count > 0 && !conn.read_shutdown) {

					

					// Post bytes to the upper layer
					UtpCallbacks.utp_call_on_read(conn.ctx, conn, data, count);
				}
				conn.ack_nr++;

				// Check if the next packet has been received too, but waiting
				// in the reorder buffer.
				for (;;) {

					if (!conn.got_fin_reached && conn.got_fin && conn.eof_pkt == conn.ack_nr) {
						conn.got_fin_reached = true;
						conn.rto_timeout = conn.ctx.current_ms + min<uint>(conn.rto * 3, 60);
						

						UtpCallbacks.utp_call_on_state_change(conn.ctx, conn, UTP_STATE_EOF);

						// if the other end wants to close, ack
						conn.send_ack();

						// reorder_count is not necessarily 0 at this point.
						// even though it is most of the time, the other end
						// may have sent packets with higher sequence numbers
						// than what later end up being eof_pkt
						// since we have received all packets up to eof_pkt
						// just ignore the ones after it.
						conn.reorder_count = 0;
					}

					// Quick get-out in case there is nothing to reorder
					if (conn.reorder_count == 0)
						break;

					// Check if there are additional buffers in the reorder buffers
					// that need delivery.
					byte *p = (byte*)conn.inbuf.get(conn.ack_nr+1);
					if (p == null)
						break;
					conn.inbuf.put(conn.ack_nr+1, null);
					count = *(uint*)p;
					if (count > 0 && !conn.read_shutdown) {
						// Pass the bytes to the upper layer
						UtpCallbacks.utp_call_on_read(conn.ctx, conn, p + sizeof(uint), count);
					}
					conn.ack_nr++;

					// Free the element from the reorder buffer
					//free(p);
					//assert(conn.reorder_count > 0);
					conn.reorder_count--;
				}

				conn.schedule_ack();
			} else {
				// Getting an out of order packet.
				// The packet needs to be remembered and rearranged later.

				// if we have received a FIN packet, and the EOF-sequence number
				// is lower than the sequence number of the packet we just received
				// something is wrong.
				if (conn.got_fin && pk_seq_nr > conn.eof_pkt) {
					return 0;
				}

				// if the sequence number is entirely off the expected
				// one, just drop it. We can't allocate buffer space in
				// the inbuf entirely varBased on untrusted input
				if (seqnr > 0x3ff) {
					return 0;
				}

				// we need to grow the circle buffer before we
				// check if the packet is already in here, so that
				// we don't end up looking at an older packet (since
				// the indices wraps around).
				conn.inbuf.ensure_size(pk_seq_nr + 1, seqnr + 1);

				// Has this packet already been received? (i.e. a duplicate)
				// If that is the case, just discard it.
				if (conn.inbuf.get(pk_seq_nr) != null) {
					return 0;
				}

				// Allocate memory to fit the packet that needs to re-ordered
				byte *mem = (byte*)malloc((packet_end - data) + sizeof(uint));
				*(uint*)mem = (uint)(packet_end - data);
				memcpy(mem + sizeof(uint), data, packet_end - data);

				// Insert into reorder buffer and increment the count
				// of # of packets to be reordered.
				// we add one to seqnr in order to leave the last
				// entry empty, that way the //assert in send_ack
				// is valid. we have to add one to seqnr too, in order
				// to make the circular buffer grow around the correct
				// point (which is conn.ack_nr + 1).
				//assert(conn.inbuf.get(pk_seq_nr) == null);
				//assert((pk_seq_nr & conn.inbuf.mask) != ((conn.ack_nr+1) & conn.inbuf.mask));
				conn.inbuf.put(pk_seq_nr, mem);
				conn.reorder_count++;

				conn.schedule_ack();
			}

			return (size_t)(packet_end - data);
		}

		public byte UTP_Version(PacketFormatV1 pf)
		{
			return (pf.type() < ST.ST_NUM_STATES && pf.ext < 3 ? pf.version() : 0);
		}



		public void UTP_FreeAll(UTPSocketHT utp_sockets) {
			utp_hash_iterator_t it;
			UTPSocketKeyData keyData;
			while ((keyData = utp_sockets.Iterate(it))) {
				delete keyData.socket;
			}
		}

		public void utp_initialize_socket(	utp_socket conn,
									 struct sockaddr addr,
									socklen_t addrlen,
									bool need_seed_gen,
									uint32 conn_seed,
									uint32 conn_id_recv,
									uint32 conn_id_send)
		{
			PackedSockAddr psaddr = PackedSockAddr((const SOCKADDR_STORAGE*)addr, addrlen);

			if (need_seed_gen) {
				do {
					conn_seed = UtpCallbacks.utp_call_get_random(conn.ctx, conn);
					// we identify v1 and higher by setting the first two bytes to 0x0001
					conn_seed &= 0xffff;
				} while (conn.ctx.utp_sockets.Lookup(UTPSocketKey(psaddr, conn_seed)));

				conn_id_recv += conn_seed;
				conn_id_send += conn_seed;
			}

			conn.state					= CS_IDLE;
			conn.conn_seed				= conn_seed;
			conn.conn_id_recv			= conn_id_recv;
			conn.conn_id_send			= conn_id_send;
			conn.addr					= psaddr;
			conn.ctx.current_ms		= UtpCallbacks.utp_call_get_milliseconds(conn.ctx, null);
			conn.last_got_packet		= conn.ctx.current_ms;
			conn.last_sent_packet		= conn.ctx.current_ms;
			conn.last_measured_delay	= conn.ctx.current_ms + 0x70000000;
			conn.average_sample_time	= conn.ctx.current_ms + 5000;
			conn.last_rwin_decay		= conn.ctx.current_ms - MAX_WINDOW_DECAY;

			conn.our_hist.clear(conn.ctx.current_ms);
			conn.their_hist.clear(conn.ctx.current_ms);
			conn.rtt_hist.clear(conn.ctx.current_ms);

			// initialize MTU floor and ceiling
			conn.mtu_reset();
			conn.mtu_last = conn.mtu_ceiling;

			conn.ctx.utp_sockets.Add(UTPSocketKey(conn.addr, conn.conn_id_recv)).socket = conn;

			// we need to fit one packet in the window when we start the connection
			conn.max_window = conn.get_packet_size();
		}

		public utp_socket utp_create_socket(utp_context ctx)
		{
			if (ctx == null) return null;

			UTPSocket conn = new UTPSocket(); // TODO: UTPSocket should have a constructor

			conn.state					= CONN_STATE.CS_UNINITIALIZED;
			conn.ctx					= ctx;
			conn.userdata				= null;
			conn.reorder_count			= 0;
			conn.duplicate_ack			= 0;
			conn.timeout_seq_nr		= 0;
			conn.last_rcv_win			= 0;
			conn.got_fin				= false;
			conn.got_fin_reached		= false;
			conn.fin_sent				= false;
			conn.fin_sent_acked		= false;
			conn.read_shutdown			= false;
			conn.close_requested		= false;
			conn.fast_timeout			= false;
			conn.rtt					= 0;
			conn.retransmit_timeout	= 0;
			conn.rto_timeout			= 0;
			conn.zerowindow_time		= 0;
			conn.average_delay			= 0;
			conn.current_delay_samples	= 0;
			conn.cur_window			= 0;
			conn.eof_pkt				= 0;
			conn.last_maxed_out_window	= 0;
			conn.mtu_probe_seq			= 0;
			conn.mtu_probe_size		= 0;
			conn.current_delay_sum		= 0;
			conn.average_delay_varBase	= 0;
			conn.retransmit_count		= 0;
			conn.rto					= 3000;
			conn.rtt_var				= 800;
			conn.seq_nr				= 1;
			conn.ack_nr				= 0;
			conn.max_window_user		= 255 * PACKET_SIZE;
			conn.cur_window_packets	= 0;
			conn.fast_resend_seq_nr	= conn.seq_nr;
			conn.target_delay			= ctx.target_delay;
			conn.reply_micro			= 0;
			conn.opt_sndbuf			= ctx.opt_sndbuf;
			conn.opt_rcvbuf			= ctx.opt_rcvbuf;
			conn.slow_start			= true;
			conn.ssthresh				= conn.opt_sndbuf;
			conn.clock_drift			= 0;
			conn.clock_drift_raw		= 0;
			conn.outbuf.mask			= 15;
			conn.inbuf.mask			= 15;
			conn.outbuf.elements		= (void**)calloc(16, sizeof(void*));
			conn.inbuf.elements		= (void**)calloc(16, sizeof(void*));
			conn.ida					= -1;	// set the index of every new socket in ack_sockets to
												// -1, which also means it is not in ack_sockets yet

			memset(conn.extensions, 0, sizeof(conn.extensions));

			// #ifdef _DEBUG
			// memset(&conn._stats, 0, sizeof(UtpCallbacks.utp_socket_stats));
			// #endif

			return conn;
		}

		public int utp_context_set_option(utp_context ctx, UTP.UTP_OPTIONS opt, int val)
		{
			if (ctx == null) return -1;

			switch (opt) {
    			case UTP.UTP_OPTIONS.UTP_LOG_NORMAL:
					ctx.log_normal = val ? true : false;
					return 0;

    			case UTP.UTP_OPTIONS.UTP_LOG_MTU:
					ctx.log_mtu = val ? true : false;
					return 0;

    			case UTP.UTP_OPTIONS.UTP_LOG_DEBUG:
					ctx.log_debug = val ? true : false;
					return 0;

    			case UTP.UTP_OPTIONS.UTP_TARGET_DELAY:
					ctx.target_delay = val;
					return 0;

				case UTP.UTP_OPTIONS.UTP_SNDBUF:
					//assert(val >= 1);
					ctx.opt_sndbuf = val;
					return 0;

				case UTP.UTP_OPTIONS.UTP_RCVBUF:
					//assert(val >= 1);
					ctx.opt_rcvbuf = val;
					return 0;
			}
			return -1;
		}

		public int utp_context_get_option(utp_context ctx, UTP.UTP_OPTIONS opt)
		{
			if (ctx == null) return -1;

			switch (opt) {
    			case UTP.UTP_OPTIONS.UTP_LOG_NORMAL:	return ctx.log_normal ? 1 : 0;
    			case UTP.UTP_OPTIONS.UTP_LOG_MTU:		return ctx.log_mtu    ? 1 : 0;
    			case UTP.UTP_OPTIONS.UTP_LOG_DEBUG:		return ctx.log_debug  ? 1 : 0;
    			case UTP.UTP_OPTIONS.UTP_TARGET_DELAY:	return ctx.target_delay;
				case UTP.UTP_OPTIONS.UTP_SNDBUF:		return ctx.opt_sndbuf;
				case UTP.UTP_OPTIONS.UTP_RCVBUF:		return ctx.opt_rcvbuf;
			}
			return -1;
		}


		public int utp_setsockopt(UTPSocket conn, UTP.UTP_OPTIONS opt, int val)
		{
			if (conn == null) return -1;

			switch (opt) {

				case UTP.UTP_OPTIONS.UTP_SNDBUF:
					//assert(val >= 1);
					conn.opt_sndbuf = val;
					return 0;

				case UTP.UTP_OPTIONS.UTP_RCVBUF:
					//assert(val >= 1);
					conn.opt_rcvbuf = val;
					return 0;

				case UTP.UTP_OPTIONS.UTP_TARGET_DELAY:
					conn.target_delay = val;
					return 0;
			}

			return -1;
		}

		public int utp_getsockopt(UTPSocket conn, UTP.UTP_OPTIONS opt)
		{
			
			if (conn == null) return -1;

			switch (opt) {
				case UTP.UTP_OPTIONS.UTP_SNDBUF:		return (int) conn.opt_sndbuf;
				case UTP.UTP_OPTIONS.UTP_RCVBUF:		return (int) conn.opt_rcvbuf;
				case UTP.UTP_OPTIONS.UTP_TARGET_DELAY:	return (int) conn.target_delay;
			}

			return -1;
		}

		// Try to connect to a specified host.
		public int utp_connect(utp_socket conn, sockaddr *to, socklen_t tolen)
		{
			if (conn == null) return -1;

			//assert(conn.state == CONN_STATE.CS_UNINITIALIZED);
			if (conn.state != CONN_STATE.CS_UNINITIALIZED) {
				conn.state = CONN_STATE.CS_DESTROY;
				return -1;
			}

			utp_initialize_socket(conn, to, tolen, true, 0, 0, 1);

			//assert(conn.cur_window_packets == 0);
			//assert(conn.outbuf.get(conn.seq_nr) == null);
			//assert(sizeof(PacketFormatV1) == 20);

			conn.state = CONN_STATE.CS_SYN_SENT;
			conn.ctx.current_ms = UtpCallbacks.utp_call_get_milliseconds(conn.ctx, conn);

			// Create and send a connect message

			

			// Setup initial timeout timer.
			conn.retransmit_timeout = 3000;
			conn.rto_timeout = conn.ctx.current_ms + conn.retransmit_timeout;
			conn.last_rcv_win = conn.get_rcv_window();

			// if you need compatibiltiy with 1.8.1, use this. it increases attackability though.
			//conn.seq_nr = 1;
			conn.seq_nr = UtpCallbacks.utp_call_get_random(conn.ctx, conn);

			// Create the connect packet.
			const size_t header_size = sizeof(PacketFormatV1);

			OutgoingPacket *pkt = (OutgoingPacket*)malloc(sizeof(OutgoingPacket) - 1 + header_size);
			PacketFormatV1* p1 = (PacketFormatV1*)pkt.data;

			memset(p1, 0, header_size);
			// SYN packets are special, and have the receive ID in the connid field,
			// instead of conn_id_send.
			p1.set_version(1);
			p1.set_type(ST_SYN);
			p1.ext = 0;
			p1.connid = conn.conn_id_recv;
			p1.windowsize = (uint32)conn.last_rcv_win;
			p1.seq_nr = conn.seq_nr;
			pkt.transmissions = 0;
			pkt.length = header_size;
			pkt.payload = 0;

			/*
			#if UTP_DEBUG_LOGGING
			conn.log(UTP_LOG_DEBUG, "Sending connect %s [%u].",
					addrfmt(conn.addr, addrbuf), conn_seed);
			#endif
			*/

			// Remember the message in the outgoing queue.
			conn.outbuf.ensure_size(conn.seq_nr, conn.cur_window_packets);
			conn.outbuf.put(conn.seq_nr, pkt);
			conn.seq_nr++;
			conn.cur_window_packets++;

			#if UTP_DEBUG_LOGGING
			conn.log(UTP_LOG_DEBUG, "incrementing cur_window_packets:%u", conn.cur_window_packets);
			#endif

			conn.send_packet(pkt);
			return 0;
		}

		// Returns 1 if the UDP payload was recognized as a UTP packet, or 0 if it was not
		public int utp_process_udp(utp_context ctx, byte *buffer, size_t len, struct sockaddr *to, socklen_t tolen)
		{
			//assert(ctx);
			if (!ctx) return 0;

			//assert(buffer);
			if (!buffer) return 0;

			//assert(to);
			if (!to) return 0;

			const PackedSockAddr addr((const SOCKADDR_STORAGE*)to, tolen);

			if (len < sizeof(PacketFormatV1)) {
				#if UTP_DEBUG_LOGGING
				ctx.log(UTP_LOG_DEBUG, null, "recv %s len:%u too small", addrfmt(addr, addrbuf), (uint)len);
				#endif
				return 0;
			}

			const PacketFormatV1 *pf1 = (PacketFormatV1*)buffer;
			const byte version = UTP_Version(pf1);
			const uint32 id = uint32(pf1.connid);

			if (version != 1) {
				#if UTP_DEBUG_LOGGING
				ctx.log(UTP_LOG_DEBUG, null, "recv %s len:%u version:%u unsupported version", addrfmt(addr, addrbuf), (uint)len, version);
				#endif

				return 0;
			}

			#if UTP_DEBUG_LOGGING
			ctx.log(UTP_LOG_DEBUG, null, "recv %s len:%u id:%u", addrfmt(addr, addrbuf), (uint)len, id);
			ctx.log(UTP_LOG_DEBUG, null, "recv id:%u seq_nr:%u ack_nr:%u", id, (uint)pf1.seq_nr, (uint)pf1.ack_nr);
			#endif

			const byte flags = pf1.type();

			if (flags == ST.ST_RESET) {
				// id is either our recv id or our send id
				// if it's our send id, and we initiated the connection, our recv id is id + 1
				// if it's our send id, and we did not initiate the connection, our recv id is id - 1
				// we have to check every case

				UTPSocketKeyData* keyData;
				if ( (keyData = ctx.utp_sockets.Lookup(UTPSocketKey(addr, id))) ||
					((keyData = ctx.utp_sockets.Lookup(UTPSocketKey(addr, id + 1))) && keyData.socket.conn_id_send == id) ||
					((keyData = ctx.utp_sockets.Lookup(UTPSocketKey(addr, id - 1))) && keyData.socket.conn_id_send == id))
				{
					UTPSocket* conn = keyData.socket;

					#if UTP_DEBUG_LOGGING
					ctx.log(UTP_LOG_DEBUG, null, "recv RST for existing connection");
					#endif

					if (conn.close_requested)
						conn.state = CONN_STATE.CS_DESTROY;
					else
						conn.state = CONN_STATE.CS_RESET;

					UtpCallbacks.utp_call_on_overhead_statistics(conn.ctx, conn, false, len + conn.get_udp_overhead(), close_overhead);
					const int err = (conn.state == CS_SYN_SENT) ? UTP_ECONNREFUSED : UTP_ECONNRESET;
					UtpCallbacks.utp_call_on_error(conn.ctx, conn, err);
				}
				else {
					#if UTP_DEBUG_LOGGING
					ctx.log(UTP_LOG_DEBUG, null, "recv RST for unknown connection");
					#endif
				}
				return 1;
			}
			else if (flags != ST_SYN) {
				UTPSocket* conn = null;

				if (ctx.last_utp_socket && ctx.last_utp_socket.addr == addr && ctx.last_utp_socket.conn_id_recv == id) {
					conn = ctx.last_utp_socket;
				} else {
					UTPSocketKeyData* keyData = ctx.utp_sockets.Lookup(UTPSocketKey(addr, id));
					if (keyData) {
						conn = keyData.socket;
						ctx.last_utp_socket = conn;
					}
				}

				if (conn) {

					#if UTP_DEBUG_LOGGING
					ctx.log(UTP_LOG_DEBUG, null, "recv processing");
					#endif

					size_t read = utp_process_incoming(conn, buffer, len);
					UtpCallbacks.utp_call_on_overhead_statistics(conn.ctx, conn, false, (len - read) + conn.get_udp_overhead(), header_overhead);
					return 1;
				}
			}

			// We have not found a matching utp_socket, and this isn't a SYN.  Reject it.
			const uint32 seq_nr = pf1.seq_nr;
			if (flags != ST.ST_SYN) {
				ctx.current_ms = utp_call_get_milliseconds(ctx, null);

				for (size_t i = 0; i < ctx.rst_info.GetCount(); i++) {
					if ((ctx.rst_info[i].connid == id)   &&
						(ctx.rst_info[i].addr   == addr) &&
						(ctx.rst_info[i].ack_nr == seq_nr))
					{
						ctx.rst_info[i].timestamp = ctx.current_ms;

						#if UTP_DEBUG_LOGGING
						ctx.log(UTP_LOG_DEBUG, null, "recv not sending RST to non-SYN (stored)");
						#endif

						return 1;
					}
				}

				if (ctx.rst_info.GetCount() > RST_INFO_LIMIT) {

					#if UTP_DEBUG_LOGGING
					ctx.log(UTP_LOG_DEBUG, null, "recv not sending RST to non-SYN (limit at %u stored)", (uint)ctx.rst_info.GetCount());
					#endif

					return 1;
				}

				#if UTP_DEBUG_LOGGING
				ctx.log(UTP_LOG_DEBUG, null, "recv send RST to non-SYN (%u stored)", (uint)ctx.rst_info.GetCount());
				#endif

				RST_Info &r = ctx.rst_info.Append();
				r.addr = addr;
				r.connid = id;
				r.ack_nr = seq_nr;
				r.timestamp = ctx.current_ms;

				send_rst(ctx, addr, id, seq_nr, utp_call_get_random(ctx, null));
				return 1;
			}

			if (ctx.callbacks[UTP_ON_ACCEPT]) {

				#if UTP_DEBUG_LOGGING
				ctx.log(UTP_LOG_DEBUG, null, "Incoming connection from %s", addrfmt(addr, addrbuf));
				#endif

				UTPSocketKeyData keyData = ctx.utp_sockets.Lookup(UTPSocketKey(addr, id + 1));
				if (keyData) {

					#if UTP_DEBUG_LOGGING
					ctx.log(UTP_LOG_DEBUG, null, "rejected incoming connection, connection already exists");
					#endif

					return 1;
				}

				if (ctx.utp_sockets.GetCount() > 3000) {

					#if UTP_DEBUG_LOGGING
					ctx.log(UTP_LOG_DEBUG, null, "rejected incoming connection, too many uTP sockets %d", ctx.utp_sockets.GetCount());
					#endif

					return 1;
				}
				// true means yes, block connection.  false means no, don't block.
				if (utp_call_on_firewall(ctx, to, tolen)) {

					#if UTP_DEBUG_LOGGING
					ctx.log(UTP_LOG_DEBUG, null, "rejected incoming connection, firewall callback returned true");
					#endif

					return 1;
				}

				// Create a new UTP socket to handle this new connection
				UTPSocket *conn = utp_create_socket(ctx);
				utp_initialize_socket(conn, to, tolen, false, id, id+1, id);
				conn.ack_nr = seq_nr;
				conn.seq_nr = utp_call_get_random(ctx, null);
				conn.fast_resend_seq_nr = conn.seq_nr;
				conn.state = CS_SYN_RECV;

				const size_t read = utp_process_incoming(conn, buffer, len, true);

				#if UTP_DEBUG_LOGGING
				ctx.log(UTP_LOG_DEBUG, null, "recv send connect ACK");
				#endif

				conn.send_ack(true);

				UtpCallbacks.utp_call_on_accept(ctx, conn, to, tolen);

				// we report overhead after on_accept(), because the callbacks are setup now
				UtpCallbacks.utp_call_on_overhead_statistics(conn.ctx, conn, false, (len - read) + conn.get_udp_overhead(), header_overhead); // SYN
				UtpCallbacks.utp_call_on_overhead_statistics(conn.ctx, conn, true,  conn.get_overhead(),                    ack_overhead);    // SYNACK
			}
			else {

				#if UTP_DEBUG_LOGGING
				ctx.log(UTP_LOG_DEBUG, null, "rejected incoming connection, UTP_ON_ACCEPT callback not set");
				#endif

			}

			return 1;
		}

		// Called by utp_process_icmp_fragmentation() and utp_process_icmp_error() below
		public static UTPSocket parse_icmp_payload(utp_context ctx, const byte *buffer, size_t len, const struct sockaddr *to, socklen_t tolen)
		{
			//assert(ctx);
			if (!ctx) return null;

			//assert(buffer);
			if (!buffer) return null;

			//assert(to);
			if (!to) return null;

			const PackedSockAddr addr((const SOCKADDR_STORAGE*)to, tolen);

			// ICMP packets are only required to quote the first 8 bytes of the layer4
			// payload.  The UDP payload is 8 bytes, and the UTP header is another 20
			// bytes.  So, in order to find the entire UTP header, we need the ICMP
			// packet to quote 28 bytes.
			if (len < sizeof(PacketFormatV1)) {
				#if UTP_DEBUG_LOGGING
				ctx.log(UTP_LOG_DEBUG, null, "Ignoring ICMP from %s: runt length %d", addrfmt(addr, addrbuf), len);
				#endif
				return null;
			}

			const PacketFormatV1 *pf = (PacketFormatV1*)buffer;
			const byte version = UTP_Version(pf);
			const uint32 id = uint32(pf.connid);

			if (version != 1) {
				#if UTP_DEBUG_LOGGING
				ctx.log(UTP_LOG_DEBUG, null, "Ignoring ICMP from %s: not UTP version 1", addrfmt(addr, addrbuf));
				#endif
				return null;
			}

			UTPSocketKeyData* keyData;

			if ( (keyData = ctx.utp_sockets.Lookup(UTPSocketKey(addr, id))) ||
				((keyData = ctx.utp_sockets.Lookup(UTPSocketKey(addr, id + 1))) && keyData.socket.conn_id_send == id) ||
				((keyData = ctx.utp_sockets.Lookup(UTPSocketKey(addr, id - 1))) && keyData.socket.conn_id_send == id))
			{
				return keyData.socket;
			}

			#if UTP_DEBUG_LOGGING
			ctx.log(UTP_LOG_DEBUG, null, "Ignoring ICMP from %s: No matching connection found for id %u", addrfmt(addr, addrbuf), id);
			#endif
			return null;
		}

		// Should be called when an ICMP Type 3, Code 4 packet (fragmentation needed) is received, to adjust the MTU
		//
		// Returns 1 if the UDP payload (delivered in the ICMP packet) was recognized as a UTP packet, or 0 if it was not
		//
		// @ctx: utp_context
		// @buf: Contents of the original UDP payload, which the ICMP packet quoted.  *Not* the ICMP packet itself.
		// @len: buffer length
		// @to: destination address of the original UDP pakcet
		// @tolen: address length
		// @next_hop_mtu: 
		public int utp_process_icmp_fragmentation(utp_context ctx, const byte* buffer, size_t len, const struct sockaddr *to, socklen_t tolen, uint16 next_hop_mtu)
		{
			UTPSocket* conn = parse_icmp_payload(ctx, buffer, len, to, tolen);
			if (!conn) return 0;

			// Constrain the next_hop_mtu to sane values.  It might not be initialized or sent properly
			if (next_hop_mtu >= 576 && next_hop_mtu < 0x2000) {
				conn.mtu_ceiling = min<uint32>(next_hop_mtu, conn.mtu_ceiling);
				conn.mtu_search_update();
				// this is something of a speecial case, where we don't set mtu_last
				// to the value in between the floor and the ceiling. We can update the
				// floor, because there might be more network segments after the one
				// that sent this ICMP with smaller MTUs. But we want to test this
				// MTU size first. If the next probe gets through, mtu_floor is updated
				conn.mtu_last = conn.mtu_ceiling;
			} else {
				// Otherwise, binary search. At this point we don't actually know
				// what size the packet that failed was, and apparently we can't
				// trust the next hop mtu either. It seems reasonably conservative
				// to just lower the ceiling. This should not happen on working networks
				// anyway.
				conn.mtu_ceiling = (conn.mtu_floor + conn.mtu_ceiling) / 2;
				conn.mtu_search_update();
			}

			conn.log(UTP_LOG_MTU, "MTU [ICMP] floor:%d ceiling:%d current:%d", conn.mtu_floor, conn.mtu_ceiling, conn.mtu_last);
			return 1;
		}

		// Should be called when an ICMP message is received that should tear down the connection.
		//
		// Returns 1 if the UDP payload (delivered in the ICMP packet) was recognized as a UTP packet, or 0 if it was not
		//
		// @ctx: utp_context
		// @buf: Contents of the original UDP payload, which the ICMP packet quoted.  *Not* the ICMP packet itself.
		// @len: buffer length
		// @to: destination address of the original UDP pakcet
		// @tolen: address length
		public int utp_process_icmp_error(utp_context ctx, const byte *buffer, size_t len, const struct sockaddr *to, socklen_t tolen)
		{
			UTPSocket* conn = parse_icmp_payload(ctx, buffer, len, to, tolen);
			if (!conn) return 0;

			const int err = (conn.state == CS_SYN_SENT) ? UTP_ECONNREFUSED : UTP_ECONNRESET;
			const PackedSockAddr addr((const SOCKADDR_STORAGE*)to, tolen);

			switch(conn.state) {
				// Don't pass on errors for idle/closed connections
				case CONN_STATE.CS_IDLE:
					#if UTP_DEBUG_LOGGING
					ctx.log(UTP_LOG_DEBUG, null, "ICMP from %s in state CS_IDLE, ignoring", addrfmt(addr, addrbuf));
					#endif
					return 1;

				default:
					if (conn.close_requested) {
						#if UTP_DEBUG_LOGGING
						ctx.log(UTP_LOG_DEBUG, null, "ICMP from %s after close, setting state to CS_DESTROY and causing error %d", addrfmt(addr, addrbuf), err);
						#endif
						conn.state = CS_DESTROY;
					} else {
						#if UTP_DEBUG_LOGGING
						ctx.log(UTP_LOG_DEBUG, null, "ICMP from %s, setting state to CS_RESET and causing error %d", addrfmt(addr, addrbuf), err);
						#endif
						conn.state = CS_RESET;
					}
					break;
			}

			UtpCallbacks.utp_call_on_error(conn.ctx, conn, err);
			return 1;
		}

		// Write bytes to the UTP socket.  Returns the number of bytes written.
		// 0 indicates the socket is no longer writable, -1 indicates an error
		ssize_t utp_writev(utp_socket conn, utp_iovec iovec_input, size_t num_iovecs)
		{
			static utp_iovec iovec[UTP_IOV_MAX];

			//assert(conn);
			if (!conn) return -1;

			//assert(iovec_input);
			if (!iovec_input) return -1;

			//assert(num_iovecs);
			if (!num_iovecs) return -1;

			if (num_iovecs > UTP_IOV_MAX)
				num_iovecs = UTP_IOV_MAX;

			memcpy(iovec, iovec_input, sizeof(struct utp_iovec)*num_iovecs);

			size_t bytes = 0;
			size_t sent = 0;
			for (size_t i = 0; i < num_iovecs; i++)
				bytes += iovec[i].iov_len;

			#if UTP_DEBUG_LOGGING
			size_t param = bytes;
			#endif

			if (conn.state != CONN_STATE.CS_CONNECTED) {
				#if UTP_DEBUG_LOGGING
				conn.log(UTP_LOG_DEBUG, "UTP_Write %u bytes = false (not CS_CONNECTED)", (uint)bytes);
				#endif
				return 0;
			}

			if (conn.fin_sent) {
				#if UTP_DEBUG_LOGGING
				conn.log(UTP_LOG_DEBUG, "UTP_Write %u bytes = false (fin_sent already)", (uint)bytes);
				#endif
				return 0;
			}

			conn.ctx.current_ms = UtpCallbacks.utp_call_get_milliseconds(conn.ctx, conn);

			// don't send unless it will all fit in the window
			size_t packet_size = conn.get_packet_size();
			size_t num_to_send = min<size_t>(bytes, packet_size);
			while (!conn.is_full(num_to_send)) {
				// Send an outgoing packet.
				// Also add it to the outgoing of packets that have been sent but not ACKed.

				bytes -= num_to_send;
				sent  += num_to_send;

				#if UTP_DEBUG_LOGGING
				conn.log(UTP_LOG_DEBUG, "Sending packet. seq_nr:%u ack_nr:%u wnd:%u/%u/%u rcv_win:%u size:%u cur_window_packets:%u",
					conn.seq_nr, conn.ack_nr,
					(uint)(conn.cur_window + num_to_send),
					(uint)conn.max_window, (uint)conn.max_window_user,
					(uint)conn.last_rcv_win, num_to_send,
					conn.cur_window_packets);
				#endif
				conn.write_outgoing_packet(num_to_send, ST_DATA, iovec, num_iovecs);
				num_to_send = min<size_t>(bytes, packet_size);

				if (num_to_send == 0) {
					#if UTP_DEBUG_LOGGING
					conn.log(UTP_LOG_DEBUG, "UTP_Write %u bytes = true", (uint)param);
					#endif
					return sent;
				}
			}

			bool full = conn.is_full();
			if (full) {
				// mark the socket as not being writable.
				conn.state = CONN_STATE.CS_CONNECTED_FULL;
			}

			#if UTP_DEBUG_LOGGING
			conn.log(UTP_LOG_DEBUG, "UTP_Write %u bytes = %s", (uint)bytes, full ? "false" : "true");
			#endif

			// returns whether or not the socket is still writable
			// if the congestion window is not full, we can still write to it
			//return !full;
			return sent;
		}

		public void utp_read_drained(utp_socket conn)
		{
			if (conn == null) return;

			if (conn.state == CONN_STATE.CS_UNINITIALIZED) return;

			size_t rcvwin = conn.get_rcv_window();

			if (rcvwin > conn.last_rcv_win) {
				// If last window was 0 send ACK immediately, otherwise should set timer
				if (conn.last_rcv_win == 0) {
					conn.send_ack();
				} else {
					conn.ctx.current_ms = UtpCallbacks.utp_call_get_milliseconds(conn.ctx, conn);
					conn.schedule_ack();
				}
			}
		}

		// Should be called each time the UDP socket is drained
		public void utp_issue_deferred_acks(utp_context ctx)
		{
			if (ctx == null) return;

			for (size_t i = 0; i < ctx.ack_sockets.GetCount(); i++) {
				UTPSocket conn = ctx.ack_sockets[i];
				conn.send_ack();
				i--;
			}
		}

		// Should be called every 500ms
		public void utp_check_timeouts(utp_context ctx)
		{
			if (ctx == null) return;

			ctx.current_ms = UtpCallbacks.utp_call_get_milliseconds(ctx, null);

			if ((long) (ctx.current_ms - ctx.last_check) < TIMEOUT_CHECK_INTERVAL)
				return;

			ctx.last_check = ctx.current_ms;

			for (size_t i = 0; i < ctx.rst_info.GetCount(); i++) {
				if ((int)(ctx.current_ms - ctx.rst_info[i].timestamp) >= RST_INFO_TIMEOUT) {
					ctx.rst_info.MoveUpLast(i);
					i--;
				}
			}
			if (ctx.rst_info.GetCount() != ctx.rst_info.GetAlloc()) {
				ctx.rst_info.Compact();
			}

			utp_hash_iterator_t it;
			UTPSocketKeyData* keyData;
			while ((keyData = ctx.utp_sockets.Iterate(it))) {
				UTPSocket conn = keyData.socket;
				conn.check_timeouts();

				// Check if the object was deleted
				if (conn.state == CONN_STATE.CS_DESTROY) {
					#if UTP_DEBUG_LOGGING
					conn.log(UTP_LOG_DEBUG, "Destroying");
					#endif
					delete conn;
				}
			}
		}

		public int utp_getpeername(utp_socket conn, sockaddr addr, socklen_t *addrlen)
		{
			//assert(addr);
			if (!addr) return -1;

			//assert(addrlen);
			if (!addrlen) return -1;

			//assert(conn);
			if (!conn) return -1;

			//assert(conn.state != CS_UNINITIALIZED);
			if (conn.state == CONN_STATE.CS_UNINITIALIZED) return -1;

			socklen_t len;
			SOCKADDR_STORAGE sa = conn.addr.get_sockaddr_storage(&len);
			*addrlen = min(len, *addrlen);
			memcpy(addr, &sa, *addrlen);
			return 0;
		}

		public int utp_get_delays(UTPSocket conn, uint32 *ours, uint32 *theirs, uint32 *age)
		{
			//assert(conn);
			if (conn == null) return -1;

			//assert(conn.state != CS_UNINITIALIZED);
			if (conn.state == CONN_STATE.CS_UNINITIALIZED) {
				if (ours)   *ours   = 0;
				if (theirs) *theirs = 0;
				if (age)    *age    = 0;
				return -1;
			}

			if (ours)   *ours   = conn.our_hist.get_value();
			if (theirs) *theirs = conn.their_hist.get_value();
			if (age)    *age    = (uint32)(conn.ctx.current_ms - conn.last_measured_delay);
			return 0;
		}

		// Close the UTP socket.
		// It is not valid for the upper layer to refer to socket after it is closed.
		// Data will keep to try being delivered after the close.
		public void utp_close(UTPSocket conn)
		{
			
			if (conn == null) return;

			//assert(conn.state != CONN_STATE.CS_UNINITIALIZED
				//&& conn.state != CONN_STATE.CS_DESTROY);

			#if UTP_DEBUG_LOGGING
			conn.log(UTP_LOG_DEBUG, "UTP_Close in state:%s", statenames[conn.state]);
			#endif

			switch(conn.state) {
			case CONN_STATE.CS_CONNECTED:
			case CONN_STATE.CS_CONNECTED_FULL:
				conn.read_shutdown = true;
				conn.close_requested = true;
				if (!conn.fin_sent) {
					conn.fin_sent = true;
					conn.write_outgoing_packet(0, ST.ST_FIN, null, 0);
				} else if (conn.fin_sent_acked) {
					conn.state = CONN_STATE.CS_DESTROY;
				}
				break;

			case CONN_STATE.CS_SYN_SENT:
				conn.rto_timeout = UtpCallbacks.utp_call_get_milliseconds(conn.ctx, conn) + min<uint>(conn.rto * 2, 60);
			// fall through
			case CONN_STATE.CS_SYN_RECV:
				// fall through
			default:
				conn.state = CONN_STATE.CS_DESTROY;
				break;
			}
		}

		public void utp_shutdown(UTPSocket conn, int how)
		{
			if (conn == null)
			{
				Console.WriteLine("utp_shutdown: conn is null");
				throw new Exception("utp_shutdown: conn is null");
			}

			if (conn.state != CONN_STATE.CS_UNINITIALIZED || conn.state != CONN_STATE.CS_DESTROY)
			{
				Console.WriteLine("conn.state should not be CS_UNITIALIZED or CS_DESTROY");
				throw new Exception("conn.state should not be CS_UNITIALIZED or CS_DESTROY");
			}
			
			

			if (how != SHUT_WR) {
				conn.read_shutdown = true;
			}
			if (how != SHUT_RD) {
				switch(conn.state) {
				case CONN_STATE.CS_CONNECTED:
				case CONN_STATE.CS_CONNECTED_FULL:
					if (!conn.fin_sent) {
						conn.fin_sent = true;
						conn.write_outgoing_packet(0, ST.ST_FIN, null, 0);
					}
					break;
				case CONN_STATE.CS_SYN_SENT:
					conn.rto_timeout = UtpCallbacks.utp_call_get_milliseconds(conn.ctx, conn) + min<uint>(conn.rto * 2, 60);
					break;
				}
			}
		}

		public utp_context utp_get_context(utp_socket socket) {
			if (socket == null)
			{
				throw new Exception("utp_get_context: socket is null");
			}

			return socket.ctx;
		}

		public void* utp_set_userdata(utp_socket socket, void* userdata) {
			if (socket == null)
			{
				throw new Exception("utp_set_userdata: socket is null");
			}

			socket.userdata = userdata;
			return socket.userdata;
		}

		public void* utp_get_userdata(utp_socket socket) {
			if (socket == null)
			{
				throw new Exception("utp_get_userdata: socket is null");
			}

			return socket.userdata;
		}

		

		

		

		public UtpCallbacks.utp_socket_stats utp_get_stats(utp_socket socket)
		{
			return null;
		}

        
        
        
        
    }
}