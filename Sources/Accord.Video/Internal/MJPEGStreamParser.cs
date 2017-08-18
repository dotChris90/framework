﻿using System;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

// AForge Video Library
// AForge.NET framework
// http://www.aforgenet.com/framework/
//
// Copyright © AForge.NET, 2005-2011
// contacts@aforgenet.com
//

namespace Accord.Video
{
    /// <summary>
    /// 
    /// </summary>
    public class MJPEGStreamParser
    {
        private const int READ_SIZE = 1024;
        private const int BUFFER_SIZE = READ_SIZE * READ_SIZE;

        private readonly byte[] _buffer;

        private int _position = 0;
        private int _totalReadBytes = 0;

        private int _imageHeaderIndex = -1;
        private int _imageBoundaryIndex = -1;

        private readonly byte[] _header;
        private readonly Boundary _boundary;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="boundary"></param>
        /// <param name="header"></param>
        /// <param name="bufferSize"></param>
        public MJPEGStreamParser(Boundary boundary, byte[] header, int bufferSize = BUFFER_SIZE)
        {
            _header = header;
            _boundary = boundary;

            _buffer = new byte[bufferSize];
        }

        /// <summary>
        /// 
        /// </summary>
        public byte[] Content
        {
            get { return _buffer; }
        }

        private int RemainingBytes
        {
            get { return _totalReadBytes - _position; }
        }
        
        private bool HasStart
        {
            get { return _imageHeaderIndex != -1; }
        }
        
        private bool HasEnd
        {
            get { return _imageBoundaryIndex != -1; }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool HasFrame
        {
            get { return HasStart && HasEnd; }
        }

        private void Add(byte[] content, int readBytes)
        {
            Array.Copy(content, 0, _buffer, _totalReadBytes, readBytes);
            _totalReadBytes += readBytes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public int Read(Stream stream)
        {
            EnsurePositionInRange();

            int readBytes;
            byte[] buffer = new byte[READ_SIZE];
            int offset = _totalReadBytes;

            readBytes = stream.Read(buffer, 0, READ_SIZE);

            if (readBytes == 0)
                throw new ApplicationException();

            Add(buffer, readBytes);

            return readBytes;
        }

        private void EnsurePositionInRange()
        {
            bool isOutOfRange = _totalReadBytes > BUFFER_SIZE - READ_SIZE;
            if (isOutOfRange)
            {
                _position = 0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void DetectImageBoundaries()
        {
            if (!HasStart && CanRead(_header))
            {
                _imageHeaderIndex = FindHeader();

                if (HasStart)
                {
                    PositionAfterHeader();
                }
                else
                {
                    PositionAtEnd();
                }
            }

            while (HasStart && !HasEnd && CanRead(_boundary))
            {
                _imageBoundaryIndex = FindBoundary();

                if (!HasEnd)
                {
                    PositionAtEnd();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Bitmap GetFrame()
        {
            PositionAtImageEnd();

            int length = _imageBoundaryIndex - _imageHeaderIndex;
            Stream imageStream = new MemoryStream(_buffer, _imageHeaderIndex, length);
            return (Bitmap)Image.FromStream(imageStream);
        }

        /// <summary>
        /// 
        /// </summary>
        public void RemoveFrame()
        {
            if(HasFrame)
            {
                _position = _imageBoundaryIndex + _boundary.Length;
                Array.Copy(_buffer, _position, _buffer, 0, RemainingBytes);

                _totalReadBytes = RemainingBytes;
                _position = 0;

                _imageHeaderIndex = -1;
                _imageBoundaryIndex = -1;
            }
        }
        
        private void PositionAtEnd()
        {
            if (_boundary.HasValue)
            {
                int boundaryPosition = _boundary.Length - 1;
                _position = _totalReadBytes - boundaryPosition;
            }
            else
            {
                _position = _totalReadBytes;
            }
        }

        private int FindHeader()
        {
            return ByteArrayUtils.Find(_buffer, _header, _position, RemainingBytes);
        }

        private int FindBoundary()
        {
            byte[] imageDelimiter;

            if (_boundary.Length != 0)
            {
                imageDelimiter = (byte[])_boundary;
            }
            else
            {
                imageDelimiter = _header;
            }

            return ByteArrayUtils.Find(_buffer, imageDelimiter, _position, RemainingBytes);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int FindImageBoundary()
        {
            return ByteArrayUtils.Find(_buffer, (byte[])_boundary, 0, RemainingBytes);
        }

        private void PositionAtImageEnd()
        {
            _position = _imageBoundaryIndex;
        }

        private void PositionAfterHeader()
        {
            _position = _imageHeaderIndex + _header.Length;
        }

        internal bool CanRead(Boundary boundary)
        {
            byte[] target = (byte[])boundary;
            return CanRead(target);
        }

        internal bool CanRead(byte[] target)
        {
            return RemainingBytes != 0 && RemainingBytes >= target.Length;
        }
    }
}
