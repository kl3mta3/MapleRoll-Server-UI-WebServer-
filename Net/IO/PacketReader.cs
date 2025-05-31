using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MapleRoll_Server_UI_.Net.IO
{
    class PacketReader : BinaryReader
    {
        private NetworkStream _stream;
        public Byte[] buffer;
        public PacketReader(NetworkStream stream) : base(stream)
        {

            _stream = stream;

        }

        public string ReadMessage()
        {
            try
            {
                //Byte[] buffer;
                var length = ReadInt32();
                buffer = new Byte[length];
                _stream.Read(buffer, 0, length);

                var message = Encoding.ASCII.GetString(buffer);

                return message;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
