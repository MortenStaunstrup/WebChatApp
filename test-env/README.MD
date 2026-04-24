This docker compose is used for creating the docker containers for intagration tests  
MongoDB instance  
Azurite for blob storage  

ENVIRONMENT VARIABLE FOR TESTING  
For integration tests, the variable MONGO_CONNECTION_STRING needs to be set to mongodb://localhost:27017 to make use of the MongoDB container instance  