using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CS422
{
    class ConcatStream : Stream
    {
        private bool _IsFirstConstructor;
        private bool _CanSeek = true;
        private long _Length = -1;
        private Stream _first;
        private Stream _second;
        private long _Position = 0;

        public ConcatStream(Stream first, Stream second)
        {
            _IsFirstConstructor = true;
            _first = first;
            _second = second;


            //First, check to see if the first stream can seek. Do not worry about the second streams length.
            //Just need to know where the first one ends and the second begins
            if (!first.CanSeek)
            {
                throw new ArgumentException();
            }

            //Set the first streams postition to 0
            _first.Position = 0;

            //Second, check if the second stream can seek. If not, then the stream is a forward-only reading stream
            if (!second.CanSeek)
            {
                _CanSeek = false;
            }
            else
            {
                //Set the second streams position to 0
                _second.Position = 0;
            }
            


        }

        public ConcatStream(Stream first, Stream second, long fixedLength)
        {
            _IsFirstConstructor = false;
            _first = first;
            _second = second;
            //First, check to see if the first stream can seek. Do not worry about the second streams length.
            //Just need to know where the first one ends and the second begins
            if (!first.CanSeek)
            {
                throw new ArgumentException();
            }
            _first.Position = 0;

            if(second.CanSeek)
            {
                _second.Position = 0;
            }

            _Length = fixedLength;
        }

        public override bool CanRead
        {
            get
            {
                if (_first.CanRead && _second.CanRead)
                {
                    return true;
                }
                return false;
            }
        }

        public override bool CanSeek
        {
            get
            {
                if (_first.CanSeek && _second.CanSeek)
                {
                    return true;
                }
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                if (_first.CanWrite && _second.CanWrite)
                {
                    return true;
                }
                return false;
            }
        }

        public override long Length
        {
            get
            {
                if (_IsFirstConstructor)
                {
                    if (_CanSeek)
                    {
                        return _first.Length + _second.Length;
                    }

                    throw new NotImplementedException();
                }
                else
                {
                    //Second constructor was used
                    return _Length;
                }

            }
        }

        public override long Position
        {
            get
            {
                return _Position;
            }

            set
            {
                //if the position is being set out of the length and the length
                if (!_CanSeek)
                {
                    throw new NotSupportedException();
                }

                if (value > _Length && _Length != -1)
                {
                    //Clamp it down
                    _Position = _Length;
                    _first.Position = _first.Length;
                    _second.Position = _second.Length;
                }
                else if(value < 0)
                {
                    //set position to 0
                    _Position = 0;
                    _first.Position = 0;
                    _second.Position = 0;
                }
                else
                {
                    _Position = value;
                    
                    //Now set the positions of the two streams based on where it lies
                    if (value < _first.Length)
                    {
                        _first.Position = value;
                        _second.Position = 0;
                    }
                    else
                    {
                        _second.Position = value - _first.Length;
                    }

                }
            }
        }



        public override void Flush()
        {
            _first.Flush();
            _second.Flush();
            _Position = 0;
            _Length = -1;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (_CanSeek)
            {
                /*MSDN: If offset is negative, the new position is required to precede the position 
             * specified by origin by the number of bytes specified by offset
            */
                if (offset < 0)
                {

                    if (origin == SeekOrigin.Current)
                        Position = _Position - offset;
                    if (origin == SeekOrigin.End)
                        Position = _Length - offset;
                    if (origin == SeekOrigin.Begin)
                        Position = 0 - offset;


                    return 0;
                }
                else if (offset == 0)
                {
                    /*MSDN: If offset is zero (0), the new position is required to be the position specified by origin*/
                    if (origin == SeekOrigin.Current)
                        return _Position;
                    if (origin == SeekOrigin.End)
                        Position = _Length;
                    if (origin == SeekOrigin.Begin)
                        Position = 0;
                }
                else
                {
                    /*MSDN: If offset is positive, the new position is required to follow the position specified 
                    * by origin by the number of bytes specified by offset. */
                    if (origin == SeekOrigin.Current)
                        Position = _Position + offset;
                    if (origin == SeekOrigin.End)
                        Position = _Length - offset;
                    if (origin == SeekOrigin.Begin)
                        Position = 0 + offset;
                    
                }
                return _Position;

            }

            throw new NotImplementedException();


        }

        public override void SetLength(long value)
        {
            //If first constructor is used, then we can set lenght. Otherwise, it is a fixed length
            if (_IsFirstConstructor)
            {
                if (value < 0)
                {
                    _Length = 0;
                }
                _Length = value;

                if (_Position > _Length && _Length != -1)
                {
                    _Position = _Length;
                }

            }

            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read_count = 0;

            //first case if the offset is less than the first stream length
            if (_Position <= _first.Length)
            {
                read_count = _first.Read(buffer, offset, count);

                //if the read_count < count then read the the rest into the second stream
                if (read_count < count)
                {
                    read_count += _second.Read(buffer, offset + read_count, count - read_count);
                }
            }
            //second case if the offset is past the length of stream 1 
            if (_Position > _first.Length)
            {
                read_count = _second.Read(buffer, offset, count);
            }

            //Adjust the position and return the count
            _Position += read_count;
            return read_count;

        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            //Check to see what stream to start writing into
            if (_Position <= _first.Length)
            {
                //First delegate the the amount of bytes to write into each stream
                int first = (int) (_first.Length - _Position);
                int second = (int)(count - first);

                _first.Write(buffer, offset, first);

                if (_Position + count > _first.Length)
                {
                    _second.Write(buffer, offset + first, second);
                }
            }

            if (_Position > _first.Length)
            {
                _second.Write(buffer, offset, count);
            }

            _Position += count;

        }
    }
}