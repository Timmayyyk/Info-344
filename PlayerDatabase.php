<?php
// Tim Davis #1332245
// Project Assignment #1
// PlayerDatabase.php

function get_data($name) {
	try {
		$conn = new PDO(
			'mysql:host=info344rds.cyqkv2epdkzi.us-west-2.rds.amazonaws.com;dbname=nbaplayersdb',
			'info344user',
			'<password>');
		$conn->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
		
		$name = "%". $name . "%";
		
		$stmt = $conn->prepare("SELECT PlayerName, GP, FGP, TPP, FTP, PPG FROM players
											WHERE PlayerName LIKE :playerName");
		$stmt->bindParam(':playerName', $name, PDO::PARAM_STR);
		$stmt->execute();

		$data = $stmt->fetchAll();
		
		return $data;
		
	} catch(PDOException $e) {
		echo("Error: " . $e->getMessage());
	}
}

?>
