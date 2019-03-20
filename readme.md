# Virtual Vacuum Robot

A simple vacuum robot to test out swarm intelligence : https://en.wikipedia.org/wiki/Swarm_intelligence

## Getting Started

- Copy **VirtualVacuumRobot\docker-compose.yml.dist** to **VirtualVacuumRobot\docker-compose.yml**
- Update **VirtualVacuumRobot\docker-compose.yml** AWS environment variables with credentials that have access to SNS and SQS.
- Build & run the docker compose command: `docker-compose up --scale vvr=3 --build` .
To tear down: `docker-compose down`.

Publish some sns messages:
Start cleaning: `{'action':'start'}`
Stop cleaning: `{'action':'stop'}`
Charge: `{'action':'charge'}`
Clear dustbin: `{'action':'dustbin'}`
Status: `{'action':'status'}`
Shutdown: `{'action':'shutdown'}`

Per vacuum robot:
Start cleaning: `{'action':'start', 'id': '1234'}`

### Prerequisites

- Docker https://www.docker.com/
- Docker compose

## Running the tests

`dotnet test`

## Built With

- [dotnet core](https://dotnet.microsoft.com/download)
- [docker](https://www.docker.com/) - Docker
- [xunit](https://rometools.github.io/rome/) - Unit testing
