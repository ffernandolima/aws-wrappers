using Amazon.SQS;
using Amazon.SQS.Model;
using AwsWrappers.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AwsWrappers
{
	public class SQS : ISQS, IDisposable
	{
		private readonly string _queueUrl;

		public IAmazonSQS SQSClient { get; protected set; }

		public SQS(Uri queueUrl)
		{
			if (queueUrl == null)
			{
				throw new ArgumentNullException("queueUrl", "Queue url cannot be null.");
			}

			this._queueUrl = queueUrl.ToString();
			this.SQSClient = new AmazonSQSClient();
		}

		public static Uri GetQueueUrl(string queueNamePrefix)
		{
			using (var sqsClient = new AmazonSQSClient())
			{
				var request = new ListQueuesRequest
				{
					QueueNamePrefix = queueNamePrefix
				};

				var response = sqsClient.ListQueues(request);

				if (response != null)
				{
					var queueUrl = response.QueueUrls.FirstOrDefault();

					if (queueUrl != null)
					{
						return new Uri(queueUrl);
					}
				}

				return null;
			}
		}

		public SendMessageResponse SendMessage(string messageBody)
		{
			var request = new SendMessageRequest
			{
				QueueUrl = this._queueUrl,
				MessageBody = messageBody
			};

			return this.SQSClient.SendMessage(request);
		}

		public List<Message> GetMessages(int maxNumberOfMessages)
		{
			// It can throw the following exception:
			// Amazon.SQS.AmazonSQSException: Value 0 for parameter MaxNumberOfMessages is invalid. Reason: Must be between 1 and 10, if provided.

			var messagesToGet = Math.Min(Math.Max(1, maxNumberOfMessages), 10);

			var request = new ReceiveMessageRequest
			{
				QueueUrl = this._queueUrl,
				MaxNumberOfMessages = messagesToGet,
				WaitTimeSeconds = 20
			};

			var response = this.SQSClient.ReceiveMessage(request);

			if (response != null)
			{
				return response.Messages;
			}

			return null;
		}

		public DeleteMessageResponse DeleteMessage(string receiptHandle)
		{
			var request = new DeleteMessageRequest
			{
				QueueUrl = this._queueUrl,
				ReceiptHandle = receiptHandle
			};

			var response = this.SQSClient.DeleteMessage(request);

			return response;
		}

		#region IDisposable Members

		private bool _disposed;

		protected virtual void Dispose(bool disposing)
		{
			if (!this._disposed)
			{
				if (disposing)
				{
					if (this.SQSClient != null)
					{
						this.SQSClient.Dispose();
						this.SQSClient = null;
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
