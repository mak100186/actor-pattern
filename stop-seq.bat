@echo off
echo Stopping and removing Seq container...

docker stop seq
docker rm seq

echo Seq container removed.
pause