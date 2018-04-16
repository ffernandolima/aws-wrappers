using Amazon.S3;
using Amazon.S3.Model;
using System.Collections.Generic;

namespace AwsWrappers.Contracts
{
	public interface IS3
	{
		IEnumerable<S3Object> GetS3Files();
		S3Object GetS3File();
		void DownloadS3File(string localPath);
		void MoveS3File(string destinationKey);
		void MoveS3File(string destinationBucket, string destinationKey);
		void UploadLocalFile(string localPath, S3StorageClass storageClass = null);
		bool ExistsS3File();
		bool ExistsS3File(string key);
	}
}
