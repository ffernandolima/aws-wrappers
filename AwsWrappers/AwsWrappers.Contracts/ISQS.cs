using Amazon.SQS.Model;
using System.Collections.Generic;

namespace AwsWrappers.Contracts
{
	public interface ISQS
	{
		SendMessageResponse SendMessage(string messageBody);
		List<Message> GetMessages(int maxNumberOfMessages);
		DeleteMessageResponse DeleteMessage(string receiptHandle);
	}
}
