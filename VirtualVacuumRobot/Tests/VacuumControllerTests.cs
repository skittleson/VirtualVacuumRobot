using Amazon.SimpleNotificationService;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using static VirtualVacuumRobot.VacuumController;

namespace VirtualVacuumRobot.Tests {

    public class VacuumControllerTests {

        private VacuumController MockVacuumController(){ 
            var sns = new Mock<IAmazonSimpleNotificationService>();
            sns.Setup(x => x.FindTopicAsync(It.IsAny<string>())).ReturnsAsync(new Amazon.SimpleNotificationService.Model.Topic(){ TopicArn = "mock" } );
            var logger = new Mock<ILogger>();
            return new VacuumController(logger.Object, sns.Object, null,false);
        }

        [Fact]
        public void Can_start() {
            // Arrange
            var vacuum = MockVacuumController();

            // Assert
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                if (eventType == VacuumEvents.STARTED) {
                    Assert.True(true);
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(2, () => vacuum.StartVacuum());
        }

        [Fact]
        public void Can_be_sent_for_charge_when_cleaning() {
            // Arrange
            var vacuum = MockVacuumController();
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                if (eventType == VacuumEvents.CLEANING) {
                    vacuum.ChargeVacuum();
                }

                // Assert
                if (eventType == VacuumEvents.STARTED_CHARGE) {
                    Assert.True(true);
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, () => vacuum.StartVacuum());
        }

        [Fact]
        public void Will_go_for_charge_if_low_on_power() {
            // Arrange
            var vacuum = MockVacuumController();
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                // Assert
                if (eventType == VacuumEvents.STARTED_CHARGE) {
                    Assert.True(true);
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, () => vacuum.StartVacuum());
        }

        [Fact]
        public void Can_send_regular_updates_on_cleaning() {
            // Arrange
            var cleanUpdates = new List<VacuumEvents>();
            var vacuum = MockVacuumController();
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
            CompleteTaskWithin(5, () => vacuum.StartVacuum());
        }

        [Fact]
        public void Can_charge() {
            // Arrange
            var vacuum = MockVacuumController();
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                // Assert
                if (eventType == VacuumEvents.STARTED_CHARGE) {
                    Assert.True(true);
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, () => vacuum.ChargeVacuum());
        }

        [Fact]
        public void After_full_charge_goes_to_sleep() {
            // Arrange
            var didSleep = false;
            var vacuum = MockVacuumController();
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                // Assert
                if (eventType == VacuumEvents.SLEEPING) {
                    didSleep = true;
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, () => vacuum.ChargeVacuum());
            Assert.True(didSleep);
        }

        [Fact]
        public void Can_get_regular_updates_while_charging() {
            // Arrange
            var updates = new List<VacuumEvents>();
            var vacuum = MockVacuumController();
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                if (eventType == VacuumEvents.CHARGING) {
                    updates.Add(eventType);
                }
                // Assert
                if (eventType == VacuumEvents.SLEEPING) {
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, () => vacuum.ChargeVacuum());
            Assert.True(updates.Count > 5);
        }
        
        [Fact]
        public void Dustbin_will_be_full_on_third_run() {
            // Arrange
            var isFull = false;
            var vacuum = MockVacuumController();
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                if (eventType == VacuumEvents.CLEANING) {
                    vacuum.Shutdown();
                }
                // Assert
                if (eventType == VacuumEvents.DUSTBIN_FULL) {
                    isFull = true;
                    vacuum.Shutdown();
                }
            };

            // Act
            CompleteTaskWithin(5, () => vacuum.StartVacuum());
            CompleteTaskWithin(5, () => vacuum.StartVacuum());
            CompleteTaskWithin(5, () => vacuum.StartVacuum());
            Assert.True(isFull);
        }
        
        [Fact]
        public void Can_get_stuck() {
            // Arrange
            var isStuck = false;
            var vacuum = MockVacuumController();
            vacuum.DustBinFullCount = 200000000;
            vacuum.ChanceOfGettingStuck = 2;
            vacuum.OnEvent += (VacuumEvents eventType, string message) => {
                if (eventType == VacuumEvents.SLEEPING) {
                    vacuum.Shutdown();
                }
                // Assert
                if (eventType == VacuumEvents.STUCK) {
                    isStuck = true;
                    vacuum.Shutdown();
                }
            };
            // Act
            for (int i = 0; i < 100; i++) {
                CompleteTaskWithin(5, () => vacuum.StartVacuum());
            }
            Assert.True(isStuck);
        }

        private void CompleteTaskWithin(int seconds, Action func) {
            var task = Task.Run(() => func());
            var completed = task.Wait(TimeSpan.FromSeconds(seconds));
            if (!completed)
                throw new Exception("Timed out");
        }
    }
}