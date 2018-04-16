using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using AwsWrappers.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace AwsWrappers
{
	public class S3 : IS3, IDisposable
	{
		private const string SLASH = @"/";
		private const string BACK_SLASH = @"\";

		public IAmazonS3 S3Client { get; protected set; }
		protected string BucketName { get; private set; }
		public string Key { get; set; }

		public S3(string bucketName)
		{
			if (string.IsNullOrWhiteSpace(bucketName))
			{
				throw new ArgumentNullException("Bucket name cannot be null or white-space.", "bucketName");
			}

			this.S3Client = new AmazonS3Client();
			this.BucketName = bucketName;
		}

		public S3(string bucketName, string key)
		{
			if (string.IsNullOrWhiteSpace(bucketName))
			{
				throw new ArgumentNullException("Bucket name cannot be null or white-space.", "bucketName");
			}

			if (string.IsNullOrWhiteSpace(key))
			{
				throw new ArgumentNullException("Key cannot be null or white-space.", "key");
			}

			this.S3Client = new AmazonS3Client();
			this.BucketName = bucketName;
			this.Key = key;
		}

		public IEnumerable<S3Object> GetS3Files()
		{
			var s3Objects = new List<S3Object>();

			try
			{
				var request = new ListObjectsRequest
				{
					BucketName = this.BucketName,
					Prefix = this.Key,
				};

				while (request != null)
				{
					var response = this.S3Client.ListObjects(request);

					if (response == null || response.S3Objects == null || !response.S3Objects.Any())
					{
						break;
					}

					s3Objects.AddRange(response.S3Objects);

					if (response.IsTruncated)
					{
						request.Marker = response.NextMarker;
					}
					else
					{
						request = null;
					}
				}
			}
			catch (AmazonS3Exception aex)
			{
				const string FORMAT = "Couldn't list files inside ({0}:/{1}).";
				var message = string.Format(FORMAT, this.BucketName, this.Key);
				throw new Exception(message, aex);
			}

			foreach (var s3Object in s3Objects)
			{
				s3Object.LastModified = s3Object.LastModified.ToUniversalTime();
			}

			return s3Objects;
		}

		public S3Object GetS3File()
		{
			S3Object s3Object;

			try
			{
				var request = new GetObjectRequest
				{
					BucketName = this.BucketName,
					Key = this.Key
				};

				var response = this.S3Client.GetObject(request);

				s3Object = new S3Object
				{
					ETag = response.ETag,
					Key = response.Key,
					LastModified = response.LastModified.ToUniversalTime()
				};
			}
			catch (AmazonS3Exception aex)
			{
				const string FORMAT = "Couldn't get file ({0}:/{1}).";
				var message = string.Format(FORMAT, this.BucketName, this.Key);
				throw new Exception(message, aex);
			}

			return s3Object;
		}

		public void DownloadS3File(string localPath)
		{
			var request = new TransferUtilityDownloadRequest
			{
				BucketName = this.BucketName,
				Key = this.Key,
				FilePath = localPath
			};

			var splitted = localPath.Split(new[] { BACK_SLASH }, StringSplitOptions.RemoveEmptyEntries);
			var directoryPath = string.Join(BACK_SLASH, splitted.Take(splitted.Length - 1));

			DirectoryHelper.CreateDirectoryIfNotExists(directoryPath);

			try
			{
				using (var response = new TransferUtility(this.S3Client))
				{
					Debug.WriteLine("Downloading " + request.Key);
					request.WriteObjectProgressEvent += TransferProgress;

					response.Download(request);
				}
			}
			catch (AmazonS3Exception aex)
			{
				const string FORMAT = "Couldn't save file ({0}:/{1}).";
				var message = string.Format(FORMAT, this.BucketName, this.Key);
				throw new FileNotFoundException(message, aex);
			}
		}

		public void MoveS3File(string destinationKey)
		{
			this.MoveS3File(this.BucketName, destinationKey);
		}

		public void MoveS3File(string destinationBucket, string destinationKey)
		{
			var copyRequest = new CopyObjectRequest
			{
				SourceBucket = this.BucketName,
				SourceKey = this.Key,
				DestinationBucket = destinationBucket,
				DestinationKey = destinationKey
			};

			var deleteRequest = new DeleteObjectRequest
			{
				BucketName = this.BucketName,
				Key = this.Key
			};

			try
			{
				var copyResponse = this.S3Client.CopyObject(copyRequest);
				var deleteResponse = this.S3Client.DeleteObject(deleteRequest);
			}
			catch (AmazonS3Exception aex)
			{
				const string FORMAT = "Couldn't move file ({0}:/{1} > {2}:/{3}).";
				var message = string.Format(FORMAT, this.BucketName, this.Key, destinationBucket, destinationKey);
				throw new FileNotFoundException(message, aex);
			}
		}

		public void UploadLocalFile(string localPath, S3StorageClass storageClass = null)
		{
			var request = new TransferUtilityUploadRequest
			{
				BucketName = this.BucketName,
				FilePath = localPath,
				StorageClass = storageClass ?? S3StorageClass.ReducedRedundancy,
				PartSize = 10 * 1024 * 1024, // 10 MB.
				Key = this.Key,
				CannedACL = S3CannedACL.BucketOwnerRead
			};

			try
			{
				using (var upper = new TransferUtility(this.S3Client))
				{
					Debug.WriteLine("Uploading " + request.Key);
					request.UploadProgressEvent += TransferProgress;

					upper.Upload(request);
				}
			}
			catch (AmazonS3Exception aex)
			{
				const string FORMAT = "Couldn't upload file ({0} > {1}:/{2}).";
				var message = string.Format(FORMAT, localPath, this.BucketName, this.Key);
				throw new Exception(message, aex);
			}
		}

		public bool ExistsS3File()
		{
			return this.ExistsS3File(this.Key);
		}

		public bool ExistsS3File(string key)
		{
			var request = new GetObjectMetadataRequest
			{
				BucketName = this.BucketName,
				Key = key
			};

			try
			{
				var response = this.S3Client.GetObjectMetadata(request);

				if (response == null || response.ContentLength < 1)
				{
					return false;
				}
			}
			catch (AmazonS3Exception amex)
			{
				if (amex.StatusCode == HttpStatusCode.NotFound)
				{
					return false;
				}

				throw;
			}

			return true;
		}

		private static void TransferProgress(object sender, TransferProgressArgs e)
		{
			var msg = string.Format("{0,3} %", e.PercentDone);
			Debug.WriteLine(msg);
		}

		#region IDisposable Members

		private bool _disposed;

		protected virtual void Dispose(bool disposing)
		{
			if (!this._disposed)
			{
				if (disposing)
				{
					if (this.S3Client != null)
					{
						this.S3Client.Dispose();
						this.S3Client = null;
					}
				}
			}

			this._disposed = true;
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		#endregion
	}
}
