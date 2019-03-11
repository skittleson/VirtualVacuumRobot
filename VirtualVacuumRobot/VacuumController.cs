using System;
using System.Collections.Generic;
using System.Threading;

namespace VirtualVacuumRobot {

    public class VacuumController {
        private readonly Random _rnd;
        private readonly int _timeInterval;
        private readonly ILogger _logger;
        private readonly QueueBroker _queueBroker;
        private bool _runtimeLoop = true;
        private bool _byMinute, _cleaningLoop, _chargingLoop;

        public double PowerPecentage { get; private set; } = 100.00;
        public Action<VacuumEvents, string> OnEvent { get; set; }

        public enum VacuumEvents {
            STARTED,
            ENDED,
            CLEANING,
            CHARGING,
            STARTED_CHARGE,
            SLEEPING,
            SHUTDOWN
        }

        public VacuumController(ILogger logger, QueueBroker queueBroker, bool byMinute = false) {
            _rnd = new Random();
            _byMinute = byMinute;
            _timeInterval = byMinute ? 1000 : 0;
            _logger = logger;
            _queueBroker = queueBroker;
            if (_queueBroker != null) {
                _queueBroker.OnEvent = FromQueueBroker;
                _queueBroker.StartListening();
            }
        }

        private void FromQueueBroker(IList<string> messages) {
            foreach (var action in messages) {
                if (action == "START") {
                    StartVaccum();
                }
            }
        }

        public void RuntimeLoop(bool startCleaning = true) {
            while (_runtimeLoop) {
                Thread.Sleep(_timeInterval);
                if (startCleaning) {
                    startCleaning = false;
                    StartVaccum();
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

        public void StartVaccum() {
            var powerPercentageDeclineRnd = _rnd.NextDouble() * (1 - .02) + .02;
            RaiseMessage(VacuumEvents.STARTED);
            _cleaningLoop = true;
            _chargingLoop = false;
            while (_runtimeLoop && _cleaningLoop && PowerPecentage > 5) {
                Thread.Sleep(_timeInterval);
                PowerPecentage -= powerPercentageDeclineRnd;
                RaiseMessage(VacuumEvents.CLEANING, PowerPecentage.ToString());
            }
            _cleaningLoop = false;
            RaiseMessage(VacuumEvents.ENDED);
            var isEndingDueToPowerPencentage = PowerPecentage <= 5;
            if (isEndingDueToPowerPencentage) {
                ChargeVaccum();
            }
        }

        public void ChargeVaccum() {
            var chargeRate = _rnd.NextDouble() * (3 - .02) + .02;
            RaiseMessage(VacuumEvents.STARTED_CHARGE);
            _cleaningLoop = false;
            _chargingLoop = true;
            PowerPecentage = 0; // Consider battery power dead.
            while (_runtimeLoop && _chargingLoop && PowerPecentage < 100) {
                Thread.Sleep(_timeInterval);
                PowerPecentage += chargeRate;
                RaiseMessage(VacuumEvents.CHARGING, PowerPecentage.ToString());
            }
            _chargingLoop = false;
            RaiseMessage(VacuumEvents.SLEEPING);
        }

        private void RaiseMessage(VacuumEvents eventType, string message = "") {
            _logger?.Log(eventType.ToString() + " " + message);
            OnEvent?.Invoke(eventType, message);
        }
    }
}