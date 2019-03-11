﻿using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace VirtualVacuumRobot {

    public class QueueBroker : IDisposable {
        private const string QUEUE_NAME = "VirtualVacuumBotQueue";
        private readonly AmazonSQSClient _client;
        private string _queueUrl { get; set; }
        private List<QueueMessageCacheModel> _cachedMessageIds { get; set; }

        public QueueBroker() {
            _cachedMessageIds = new List<QueueMessageCacheModel>();
            _client = new AmazonSQSClient();
            try {
                var findResponse = _client.GetQueueUrlAsync(new GetQueueUrlRequest(QUEUE_NAME)).Result;
                _queueUrl = findResponse.QueueUrl;
            } catch (Exception ex) {
                var createdRequest = new CreateQueueRequest(QUEUE_NAME) {
                    Attributes = new Dictionary<string, string> {
                        { QueueAttributeName.MessageRetentionPeriod, "120" }
                    }
                };
                var createdResponse = _client.CreateQueueAsync(createdRequest).Result;
                if ((int)createdResponse.HttpStatusCode == 200) {
                    _queueUrl = createdResponse.QueueUrl;
                } else {
                    throw new Exception("Unable to create queue");
                }
            }
        }

        public void SubscribeToSnsTopic() {

        }

        public void UnsubscribeToSnsTopic() {

        }

        public IEnumerable<string> ReceiveMessages() {
            var response = _client.ReceiveMessageAsync(_queueUrl).Result;
            var messages = response.Messages.Select(x => {
                var message = "";
                if (x.Body.StartsWith('{')) {
                    dynamic jsonObj = JsonConvert.DeserializeObject<ExpandoObject>(x.Body);
                    message = jsonObj.Message;
                } else {
                    message = x.Body;
                }
                return new QueueMessageCacheModel(x.MessageId, message);
            }).ToList();
            var result = messages.Where(p => !_cachedMessageIds.Any(p2 => p2.MessageId == p.MessageId)).ToList();
            if (result.Count > 0) {
                _cachedMessageIds.AddRange(result);
            }
            return result.Select(x => x.Message).ToList();
        }

        public void Dispose() {
            _client?.Dispose();
        }
    }

    public class QueueMessageCacheModel {
        public string MessageId { get; }
        public string Message { get; }
        public DateTime Received { get; }

        public QueueMessageCacheModel(string messageId, string message) {
            Message = message;
            MessageId = messageId;
            Received = DateTime.UtcNow;
        }
    }
}