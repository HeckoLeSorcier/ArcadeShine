@echo off
cd "%~p1"
start /wait "" "%~n1%~x1"
exit