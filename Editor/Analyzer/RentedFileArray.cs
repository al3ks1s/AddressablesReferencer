using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;

// MIT License Copyright (c) 2026 flibber-hk - https://github.com/flibber-hk/AssetHelperLib/blob/main/AssetHelperLib/IO/RentedFileArray.cs

namespace AddressableReferencer.Editor.Analyzer { 
    /// <summary>
    /// Class representing a file in memory that is backed by an array rented from
    /// a <see cref="ArrayPool{T}"/>.
    /// 
    /// This instance MUST be disposed, either manually or via the <see langword="using"/> keyword,
    /// when it is no longer in use.
    /// </summary>
    /// <remarks>
    /// Not putting the file into memory can cause massive performance hits because AssetsTools.NET
    /// does a lot of reads. But reading it to a byte array can cause memory pressure in
    /// Unity Mono. We utilise an array pool to mitigate this.
    /// </remarks>
    public class RentedFileArray : IDisposable
    {
        private static ArrayPool<byte> _pool = ArrayPool<byte>.Shared;

        /// <summary>
        /// The array pool used to create instances of this class.
        /// 
        /// This should be set before any files are read and unset after
        /// all file IO is finished.
        /// 
        /// Setting this is not thread safe.
        /// </summary>
        [AllowNull]
        public static ArrayPool<byte> Pool
        {
            get => _pool;
            set => _pool = value ?? ArrayPool<byte>.Shared;
        }

        private int _length;
        private ArrayPool<byte> _owner;
        private byte[] _buffer;

        /// <summary>
        /// A memory stream holding the data.
        /// 
        /// This stream will be disposed when this <see cref="RentedFileArray"/> instance is disposed,
        /// and should not be disposed manually.
        /// </summary>
        public MemoryStream Stream { get; set; }
        
        /// <summary>
        /// Construct an instance with data from the given filepath.
        /// </summary>
        /// <param name="filepath"></param>
        public RentedFileArray(string filepath)
        {
            _length = (int)new FileInfo(filepath).Length;
            _owner = Pool;
            _buffer = Pool.Rent(_length);

            try
            {
                using (FileStream fs = File.OpenRead(filepath))
                {
                    fs.Read(_buffer, 0, _length);
                }
            }
            catch
            {
                _owner.Return(_buffer);
                throw;
            }
        

            Stream = new MemoryStream(_buffer, 0, _length);
        }

        private bool _isDisposed;

        /// <inheritdoc />
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Stream.Dispose();
                    _owner.Return(_buffer);
                    _owner = null!;
                    _buffer = null!;
                }

                _isDisposed = true;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
