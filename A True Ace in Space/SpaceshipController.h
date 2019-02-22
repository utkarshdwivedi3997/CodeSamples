#pragma once

#include "GameFramework/Pawn.h"
#include "SpaceshipController.generated.h"

UCLASS()
class CODENAME_TRACE_API ASpaceshipController : public APawn
{
	GENERATED_BODY()

	//Static mesh of the spaceship
	UPROPERTY(Category = Mesh, EditAnywhere)
	class UStaticMeshComponent* spaceshipMesh;

	//Camera offset
	UPROPERTY(Category = Camera, VisibleDefaultsOnly)
		class USpringArmComponent* springArm;

	//Camera that is the player's view
	UPROPERTY(Category = Camera, VisibleDefaultsOnly)
		class UCameraComponent* playerCamera;

public:
	ASpaceshipController();

	// Called when the game starts or when spawned
	virtual void BeginPlay() override;

	// Called every frame
	virtual void Tick(float DeltaSeconds) override;

	FORCEINLINE class UStaticMeshComponent* GetSpaceshipMesh() const { return spaceshipMesh; }
	FORCEINLINE class USpringArmComponent* GetSpringArm() const { return springArm; }
	FORCEINLINE class UCameraComponent* GetPlayerCamera() const { return playerCamera; }

protected:
	// Called to bind functionality to input
	virtual void SetupPlayerInputComponent(class UInputComponent* PlayerInputComponent) override;
	void ThrustInput(float val);
	void PitchInput(float val);
	void RollInput(float val);
	void YawInput(float val);
	void StrafeX(float val);
	void StrafeY(float val);
	void StartBoost();
	void StopBoost();

	UFUNCTION()
		void OnHit(UPrimitiveComponent* HitComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, FVector NormalImpulse, const FHitResult &Hit);
		
	UPROPERTY(Category = Movement, BlueprintReadWrite, VisibleAnywhere)
		float spaceshipVelocity;

private:

	// these values don't need to go into blueprints, so it's fine if they stay private

	UPROPERTY(Category = Movement, EditAnywhere)
		float maxSpeed;
	UPROPERTY(Category = Movement, EditAnywhere)
		float minSpeed;
	UPROPERTY(Category = Movement, EditAnywhere)
		float maxPitchSpeed;
	UPROPERTY(Category = Movement, EditAnywhere)
		float maxRollSpeed;
	UPROPERTY(Category = Movement, EditAnywhere)
		float maxYawSpeed;
	UPROPERTY(Category = Movement, EditAnywhere)
		float boostSpeed;
	UPROPERTY(Category = Movement, EditAnywhere)
		float boostAcceleration;
	UPROPERTY(Category = Movement, EditAnywhere)
		float cameraToSpaceshipDistance;
	UPROPERTY(Category = Movement, EditAnywhere)
		float maxXStrafeSpeed;
	UPROPERTY(Category = Movement, EditAnywhere)
		float maxYStrafeSpeed;

	float currentForwardSpeed, currentPitchSpeed, currentRollSpeed, currentYawSpeed,  rotationModifier, currentBoostSpeed, currentBoostAcceleration;
	float currentXStrafeSpeed, currentYStrafeSpeed;
	bool bIsBoosting;
};