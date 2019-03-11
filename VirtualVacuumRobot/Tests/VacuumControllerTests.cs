using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using static VirtualVacuumRobot.VacuumController;

namespace VirtualVacuumRobot.Tests {

    public class VacuumControllerTests {

        [Fact]
        public void Can_start() {
            // Arrange
            var vacuum = new VacuumController(null, false);

            // Assert
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                if (eventType == VacuumEvents.STARTED) {
                    Assert.True(true);
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(2, vacuum.StartVaccum);
        }

        [Fact]
        public void Can_be_sent_for_charge_when_cleaning() {
            // Arrange
            var vacuum = new VacuumController(null, false);
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                if (eventType == VacuumEvents.CLEANING) {
                    vacuum.ChargeVaccum();
                }

                // Assert
                if (eventType == VacuumEvents.STARTED_CHARGE) {
                    Assert.True(true);
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, vacuum.StartVaccum);
        }

        [Fact]
        public void Will_go_for_charge_if_low_on_power() {
            // Arrange
            var vacuum = new VacuumController(null, false);
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                // Assert
                if (eventType == VacuumEvents.STARTED_CHARGE) {
                    Assert.True(true);
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, vacuum.StartVaccum);
        }

        [Fact]
        public void Can_send_regular_updates_on_cleaning() {
            // Arrange
            var cleanUpdates = new List<VacuumEvents>();
            var vacuum = new VacuumController(null, false);
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                if (eventType == VacuumEvents.CLEANING) {
                    cleanUpdates.Add(eventType);
                }

                // Assert
                if (eventType == VacuumEvents.ENDED) {
                    Assert.True(cleanUpdates.Count > 5);
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, vacuum.StartVaccum);
        }

        [Fact]
        public void Can_charge() {
            // Arrange
            var vacuum = new VacuumController(null, false);
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                // Assert
                if (eventType == VacuumEvents.STARTED_CHARGE) {
                    Assert.True(true);
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, vacuum.ChargeVaccum);
        }

        [Fact]
        public void After_full_charge_goes_to_sleep() {
            // Arrange
            var vacuum = new VacuumController(null, false);
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                // Assert
                if (eventType == VacuumEvents.SLEEPING) {
                    Assert.True(true);
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, vacuum.ChargeVaccum);
        }

        [Fact]
        public void Can_get_regular_updates_while_charging() {
            // Arrange
            var updates = new List<VacuumEvents>();
            var vacuum = new VacuumController(null, false);
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                if (eventType == VacuumEvents.CHARGING) {
                    updates.Add(eventType);
                }
                // Assert
                if (eventType == VacuumEvents.SLEEPING) {
                    Assert.True(updates.Count > 5);
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, vacuum.ChargeVaccum);
        }

        private void CompleteTaskWithin(int seconds, Action func) {
            var task = Task.Run(() => func());
            var completed = task.Wait(TimeSpan.FromSeconds(seconds));
            if (!completed)
                throw new Exception("Timed out");
        }
    }
}