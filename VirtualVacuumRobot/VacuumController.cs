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
            SHUTDOWN
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
            if(_queueBroker != null) {
                _queueBroker.Subscribe(_sns, _snsTopics["VirtualVacuumRobot"]);
                _queueBroker.OnEvent = FromQueueBroker;
                _queueBroker.StartListening();
            }
            PowerPecentage = _rnd.Next(65, 100);
        }

        private void FromQueueBroker(IList<string> messages) {
            foreach(var message in messages) {
                if(message.StartsWith('{')) {
                    try {
                        var messageRequest = JsonConvert.DeserializeObject<VirtualVacuumRobotSqsMessage>(message);
                        if(messageRequest.Id == _id.ToString() || messageRequest.Id == "") {
                            if(string.Equals(messageRequest.Action, "start", StringComparison.CurrentCultureIgnoreCase)) {
                                _cleaningLoop = true;
                            }
                            if(string.Equals(messageRequest.Action, "stop", StringComparison.CurrentCultureIgnoreCase)) {
                                _cleaningLoop = false;
                            }
                        }
                    } catch(Exception ex) {
                        _logger.Log(message + ex.GetBaseException().ToString());
                        break;
                    }
                } else {
                    if(message == "START" && !_cleaningLoop) {
                        _cleaningLoop = true;
                    } else if(message == "CHARGE" && !_chargingLoop) {
                        _chargingLoop = true;
                    }
                }
            }
        }

        public void RuntimeLoop() {
            while(_runtimeLoop) {
                Thread.Sleep(_timeInterval);
                if(_cleaningLoop) {
                    StartVacuum();
                }
                if(_chargingLoop) {
                    ChargeVacuum();
                }
                RaiseMessage(VacuumEvents.SLEEPING);
            }
        }

        public void Shutdown() {
            RaiseMessage(VacuumEvents.SHUTDOWN);
            _runtimeLoop = false;
            _cleaningLoop = false;
            _chargingLoop = false;
        }

        public void StartVacuum() {
            _runCount++;
            var powerPercentageDeclineRnd = _rnd.NextDouble() * (1 - .02) + .02;
            RaiseMessage(VacuumEvents.STARTED);
            _cleaningLoop = true;
            _chargingLoop = false;
            while(_cleaningLoop && PowerPecentage > 5) {
                Thread.Sleep(_timeInterval);
                if(IsStuck()) {
                    RaiseMessage(VacuumEvents.STUCK, PowerPecentage.ToString());
                    _cleaningLoop = false;
                    break;
                }
                if(_runCount > DustBinFullCount) {
                    RaiseMessage(VacuumEvents.DUSTBIN_FULL, PowerPecentage.ToString());
                    _cleaningLoop = false;
                    break;
                }
                PowerPecentage -= powerPercentageDeclineRnd;
                RaiseMessage(VacuumEvents.CLEANING, PowerPecentage.ToString());
            }
            _cleaningLoop = false;
            RaiseMessage(VacuumEvents.ENDED);
            var isEndingDueToPowerPercentage = PowerPecentage <= 5;
            if(isEndingDueToPowerPercentage) {
                _chargingLoop = true;
            }
        }

        public void ChargeVacuum() {
            var chargeRate = _rnd.NextDouble() * (3 - .02) + .02;
            RaiseMessage(VacuumEvents.STARTED_CHARGE);
            _cleaningLoop = false;
            _chargingLoop = true;
            PowerPecentage = 0; // Consider battery power dead.
            while(_chargingLoop && PowerPecentage < 100) {
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
                } catch(Exception ex) {
                    try {
                        topicArn = _sns.CreateTopicAsync(new CreateTopicRequest {
                            Name = topic
                        }).Result.TopicArn;
                    } catch(Exception ex2) {
                        _logger.Log(ex.GetBaseException().ToString());
                        throw ex2;
                    }
                }
                return new KeyValuePair<string, string>(topic, topicArn);
            }).ToDictionary(k => k.Key, v => v.Value);
        }

        private void RaiseMessage(VacuumEvents eventType, string message = "") {
            dynamic messageObject = new {
                id = _id,
                message,
                eventType = eventType.ToString("g")
            };
            var messageRequest = JsonConvert.SerializeObject(messageObject);
            _logger?.Log(messageRequest);
            OnEvent?.Invoke(eventType, message);
            var topicArnToNotify = _snsTopics[SNS_TOPIC_GENERAL];
            if(eventType == VacuumEvents.DUSTBIN_FULL || eventType == VacuumEvents.STUCK) {
                topicArnToNotify = _snsTopics["VirtualVacuumRobot_" + eventType.ToString()];
            }
            try {
                _sns.PublishAsync(topicArnToNotify, messageRequest).Wait();
            } catch(Exception ex) {
                _logger.Log(ex.GetBaseException().ToString());
            }
        }

        internal class VirtualVacuumRobotSqsMessage {
            public string Action { get; set; }
            public string Id { get; set; }
        }
    }
}