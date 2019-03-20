using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace VirtualVacuumRobot {

    public class VacuumController {
        private readonly Random _rnd;
        private readonly int _timeInterval;
        private readonly ILogger _logger;
        private readonly QueueBroker _queueBroker;
        private IAmazonSimpleNotificationService _sns;
        private bool _runtimeLoop = true;
        private bool _byMinute, _cleaningLoop, _chargingLoop;
        private int _id;
        private int _runCount;

        public int DustBinFullCount = 2;
        public double PowerPecentage { get; private set; }
        public Action<VacuumEvents, string> OnEvent { get; set; }
        public int ChanceOfGettingStuck { get; set; } = 1000;

        public enum VacuumEvents {
            DUSTBIN_FULL,
            STARTED,
            ENDED,
            CLEANING,
            CHARGING,
            STARTED_CHARGE,
            SLEEPING,
            STUCK,
            SHUTDOWN,
            STATUS,
            READY
        }

        private const string SNS_TOPIC_GENERAL = "VirtualVacuumRobot_General";

        private Dictionary<string, string> _snsTopics = new Dictionary<string, string>(){
            {"VirtualVacuumRobot" ,"" },
            {"VirtualVacuumRobot_" + VacuumEvents.DUSTBIN_FULL.ToString() ,"" },
            {"VirtualVacuumRobot_" + VacuumEvents.STUCK.ToString() ,"" },
            {SNS_TOPIC_GENERAL,"" },
        };

        public VacuumController(ILogger logger, IAmazonSimpleNotificationService sns, QueueBroker queueBroker, bool byMinute = false) {
            _rnd = new Random();
            _id = _rnd.Next(100, 10000);
            _byMinute = byMinute;
            _timeInterval = byMinute ? 1000 : 0;
            _logger = logger;
            _runCount = 0;
            _queueBroker = queueBroker;
            _sns = sns;
            FindOrCreateSnsTopics();
            if (_queueBroker != null) {
                _queueBroker.CreateQueue(_id);
                _queueBroker.Subscribe(_sns, _snsTopics["VirtualVacuumRobot"]);
                _queueBroker.OnEvent = FromQueueBroker;
                _queueBroker.StartListening();
            }
            PowerPecentage = _rnd.Next(65, 100);
        }

        private void FromQueueBroker(IList<string> messages) {
            foreach (var message in messages) {
                if (message.StartsWith('{')) {
                    try {
                        var messageRequest = JsonConvert.DeserializeObject<VirtualVacuumRobotSqsMessage>(message);
                        if (messageRequest.Id == _id.ToString() || String.IsNullOrEmpty(messageRequest.Id)) {
                            if (IsSame(messageRequest.Action, "start") && !_cleaningLoop) {
                                _cleaningLoop = true;
                            }
                            if (IsSame(messageRequest.Action, "stop")) {
                                _cleaningLoop = false;
                            }
                            if (IsSame(messageRequest.Action, "charge") && !_chargingLoop) {
                                _chargingLoop = true;
                            }
                            if (IsSame(messageRequest.Action, "dustbin")) {
                                _runCount = 0;
                            }
                            if (IsSame(messageRequest.Action, "status")) {
                                RaiseMessage(VacuumEvents.STATUS, PowerPecentage.ToString());
                            }
                            if (IsSame(messageRequest.Action, "shutdown")) {
                                Shutdown();
                            }
                            if (IsSame(messageRequest.Action, "teardown")) {
                                Shutdown();
                                DeleteAllSnsTopics();
                                _queueBroker?.DeleteQueuesWithPrefix();
                            }
                        }
                    } catch (Exception ex) {
                        _logger.Log(message + ex.GetBaseException().ToString());
                        break;
                    }
                }
            }
        }

        public void RuntimeLoop() {
            RaiseMessage(VacuumEvents.READY, PowerPecentage.ToString());
            while (_runtimeLoop) {
                Thread.Sleep(_timeInterval);
                if (_cleaningLoop) {
                    StartVacuum();
                }
                if (_chargingLoop) {
                    ChargeVacuum();
                }
            }
        }

        public void Shutdown() {
            RaiseMessage(VacuumEvents.SHUTDOWN);
            _cleaningLoop = false;
            _chargingLoop = false;
            if (_queueBroker != null) {
                _queueBroker.StopListening();
                _queueBroker.DeleteQueue();
                try {
                    _sns.UnsubscribeAsync(_queueBroker.QueueUrlSnsTopicSubscribeArn);
                } catch(Exception ex) {
                    // could have been torn down already
                }                
            }
            _runtimeLoop = false;
        }

        public void StartVacuum() {
            _runCount++;
            var powerPercentageDeclineRnd = _rnd.NextDouble() * (1 - .08) + .08;
            RaiseMessage(VacuumEvents.STARTED);
            _cleaningLoop = true;
            _chargingLoop = false;
            while (_cleaningLoop) {
                Thread.Sleep(_timeInterval);
                if (PowerPecentage < 5) {
                    _chargingLoop = true;
                }
                if (_chargingLoop) {
                    break;
                }
                if (IsStuck()) {
                    RaiseMessage(VacuumEvents.STUCK, PowerPecentage.ToString());
                    _cleaningLoop = false;
                    break;
                }
                if (_runCount > DustBinFullCount) {
                    RaiseMessage(VacuumEvents.DUSTBIN_FULL, PowerPecentage.ToString());
                    _cleaningLoop = false;
                    break;
                }
                PowerPecentage -= powerPercentageDeclineRnd;
                RaiseMessage(VacuumEvents.CLEANING, PowerPecentage.ToString());
            }
            _cleaningLoop = false;
            RaiseMessage(VacuumEvents.ENDED);
        }

        public void ChargeVacuum() {
            var chargeRate = _rnd.NextDouble() * (3 - .02) + .02;
            RaiseMessage(VacuumEvents.STARTED_CHARGE);
            _cleaningLoop = false;
            _chargingLoop = true;
            PowerPecentage = 0; // Consider battery power dead.
            while (_chargingLoop && PowerPecentage < 100) {
                Thread.Sleep(_timeInterval);
                PowerPecentage += chargeRate;
                RaiseMessage(VacuumEvents.CHARGING, PowerPecentage.ToString());
            }
            _chargingLoop = false;
            RaiseMessage(VacuumEvents.SLEEPING);
        }

        private bool IsStuck() {
            int rand = _rnd.Next(0, ChanceOfGettingStuck);
            return rand == 1;
        }

        private void FindOrCreateSnsTopics() {
            _snsTopics = _snsTopics.Select(topicKeyValue => {
                var topic = topicKeyValue.Key;
                var topicArn = "";
                try {
                    topicArn = _sns.FindTopicAsync(topic).Result.TopicArn;
                } catch (Exception ex) {
                    try {
                        topicArn = _sns.CreateTopicAsync(new CreateTopicRequest {
                            Name = topic
                        }).Result.TopicArn;
                    } catch (Exception ex2) {
                        _logger.Log(ex.GetBaseException().ToString());
                        throw ex2;
                    }
                }
                return new KeyValuePair<string, string>(topic, topicArn);
            }).ToDictionary(k => k.Key, v => v.Value);
        }

        private void DeleteAllSnsTopics(){
            foreach(var snsTopic in _snsTopics) {
                try {
                    _sns.DeleteTopicAsync(snsTopic.Value).Wait();
                } catch(Exception ex) {
                    _logger.Log("Unable to delete: " + snsTopic);
                }
            }
        }

        private void RaiseMessage(VacuumEvents eventType, string message = "") {
            dynamic messageObject = new {
                id = _id,
                message,
                eventType = eventType.ToString("g"),
                timestamp = DateTime.UtcNow
            };
            var messageRequest = JsonConvert.SerializeObject(messageObject);
            _logger?.Log(messageRequest);
            OnEvent?.Invoke(eventType, message);
            var topicArnToNotify = _snsTopics[SNS_TOPIC_GENERAL];
            if (eventType == VacuumEvents.DUSTBIN_FULL || eventType == VacuumEvents.STUCK) {
                topicArnToNotify = _snsTopics["VirtualVacuumRobot_" + eventType.ToString()];
            }
            try {
                _sns.PublishAsync(topicArnToNotify, messageRequest).Wait();
            } catch (Exception ex) {
                _logger.Log(ex.GetBaseException().ToString());
            }
        }

        private bool IsSame(string requestedCommand, string command) {
            return string.Equals(requestedCommand, command, StringComparison.CurrentCultureIgnoreCase);
        }

        internal class VirtualVacuumRobotSqsMessage {
            public string Action { get; set; }
            public string Id { get; set; }
        }
    }
}