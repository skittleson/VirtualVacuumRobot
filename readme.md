# Virtual Vacuum Robot

A simple vacuum robot to test out swarm intelligence : https://en.wikipedia.org/wiki/Swarm_intelligence

## Getting Started

- Copy **VirtualVacuumRobot\docker-compose.yml.dist** to **VirtualVacuumRobot\docker-compose.yml**  
- Update **VirtualVacuumRobot\docker-compose.yml**  AWS environment variables with credentials that have access to SNS and SQS. 
- Run the docker compose command:  `docker-compose up --scale vvr=3` . 

### Prerequisites

- Docker https://www.docker.com/
- Docker compose

## Running the tests

`dotnet test`

## Built With

* [dotnet core](https://dotnet.microsoft.com/download)
* [docker](https://www.docker.com/) - Docker
* [xunit](https://rometools.github.io/rome/) - Unit testing

