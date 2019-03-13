using Amazon.SimpleNotificationService;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace VirtualVacuumRobot {

    public class VacuumController {
        private readonly Random _rnd;
        private readonly int _timeInterval;
        private readonly ILogger _logger;
        private readonly QueueBroker _queueBroker;
        private readonly IAmazonSimpleNotificationService _sns;
        private bool _runtimeLoop = true;
        private bool _byMinute, _cleaningLoop, _chargingLoop;
        private int _id;
        private int _runCount;

        public double PowerPecentage { get; private set; }
        public Action<VacuumEvents, string> OnEvent { get; set; }

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

        public VacuumController(ILogger logger, IAmazonSimpleNotificationService sns, QueueBroker queueBroker, bool byMinute = false) {
            _rnd = new Random();
            _id = _rnd.Next(100, 10000);
            _byMinute = byMinute;
            _timeInterval = byMinute ? 1000 : 0;
            _logger = logger;
            _runCount = 0;
            _queueBroker = queueBroker;
            _sns = sns;
            if(_queueBroker != null) {
                _queueBroker.OnEvent = FromQueueBroker;
                _queueBroker.StartListening();
            }
            PowerPecentage = _rnd.Next(65, 100);
        }

        private void FromQueueBroker(IList<string> messages) {
            foreach(var action in messages) {
                if(action == "START") {
                    _cleaningLoop = true;
                } else if(action == "CHARGE") {
                    _chargingLoop = true;
                }
            }
        }

        public void RuntimeLoop(bool startCleaning = true) {
            while(_runtimeLoop) {
                Thread.Sleep(_timeInterval);
                if(startCleaning || _cleaningLoop) {
                    startCleaning = false;
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

        public void StartVacuum()
        {
            _runCount++;
            var powerPercentageDeclineRnd = _rnd.NextDouble() * (1 - .02) + .02;
            RaiseMessage(VacuumEvents.STARTED);
            _cleaningLoop = true;
            _chargingLoop = false;
            while(_cleaningLoop && PowerPecentage > 5) {
                Thread.Sleep(_timeInterval);
                if (IsStuck()) {
                    RaiseMessage(VacuumEvents.STUCK, PowerPecentage.ToString());
                    break;
                }
                if (_runCount > 1) {
                    RaiseMessage(VacuumEvents.DUSTBIN_FULL, PowerPecentage.ToString());
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

        private Boolean IsStuck() {
            int rand = _rnd.Next(0, 1000);
            return rand == 1;
        }

        private void RaiseMessage(VacuumEvents eventType, string message = "") {
            _logger?.Log(eventType.ToString() + " " + message);
            OnEvent?.Invoke(eventType, message);
            try {
                Task.Run(() => _sns.PublishAsync("", ""));
            } catch(Exception ex) {
                _logger.Log(ex.GetBaseException().ToString());
            }
        }
    }
}